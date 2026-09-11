#include <dlfcn.h>
#include <libgen.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef void* hostfxr_handle;
struct hostfxr_initialize_parameters
{
	size_t size;
	char *host_path;
	char *dotnet_root;
};

typedef int32_t(*hostfxr_initialize_for_dotnet_command_line_fn)(
	int argc, char **argv,
	struct hostfxr_initialize_parameters *parameters,
	hostfxr_handle *host_context_handle);
typedef int32_t(*hostfxr_run_app_fn)(const hostfxr_handle host_context_handle);
typedef int32_t(*hostfxr_close_fn)(const hostfxr_handle host_context_handle);

static char *find_dotnet_root(char *hostfxr_path)
{
	const char *env = getenv("DOTNET_ROOT");
	if (env && env[0])
		return strdup(env);

	/* .../dotnet/host/fxr/VERSION/libhostfxr.dylib -> .../dotnet */
	char *copy = strdup(hostfxr_path);
	char *p = copy;
	for (int i = 0; i < 4; i++)
	{
		char *slash = strrchr(p, '/');
		if (!slash)
			break;
		*slash = 0;
	}
	return copy;
}

int main(int argc, char **argv)
{
	if (argc < 3)
	{
		fprintf(stderr, "Usage: %s <libhostfxr.dylib> <OpenRA.dll> [args...]\n", argv[0]);
		return 1;
	}

	/* Allow net6 game on newer runtimes when launched outside a shell. */
	setenv("DOTNET_ROLL_FORWARD", "LatestMajor", 0);

	void *lib = dlopen(argv[1], RTLD_LAZY);
	if (lib == NULL)
	{
		fprintf(stderr, "Failed to load %s: %s\n", argv[1], dlerror());
		return 1;
	}

	hostfxr_initialize_for_dotnet_command_line_fn init_fn =
		(hostfxr_initialize_for_dotnet_command_line_fn)dlsym(lib, "hostfxr_initialize_for_dotnet_command_line");
	hostfxr_run_app_fn run_fn = (hostfxr_run_app_fn)dlsym(lib, "hostfxr_run_app");
	hostfxr_close_fn close_fn = (hostfxr_close_fn)dlsym(lib, "hostfxr_close");
	if (!init_fn || !run_fn || !close_fn)
	{
		fprintf(stderr, "Could not load hostfxr exports: %s\n", dlerror());
		return 1;
	}

	struct hostfxr_initialize_parameters params;
	params.size = sizeof(params);
	params.host_path = argv[0];
	params.dotnet_root = find_dotnet_root(argv[1]);

	hostfxr_handle handle = NULL;
	int32_t rc = init_fn(argc - 2, &argv[2], &params, &handle);
	if (rc != 0 || handle == NULL)
	{
		fprintf(stderr, "hostfxr_initialize failed: %d (DOTNET_ROOT=%s)\n", rc, params.dotnet_root);
		return 1;
	}

	run_fn(handle);
	return close_fn(handle);
}
