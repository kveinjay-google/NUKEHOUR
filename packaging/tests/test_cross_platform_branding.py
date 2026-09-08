import re
import subprocess
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]

HISTORICAL_PREFIXES = ("docs/", "engine-patches/", "patches/")
AUDIT_GUARDS = {
	"packaging/ios_app_brand_audit.py",
	"packaging/ios_loading_brand_audit.py",
}
STABLE_TECHNICAL_IDENTIFIERS = (
	re.compile(
		r"(?<![A-Za-z0-9_-])NUCLEAR-CRISIS-BG-0[1-9]\.png"
		r"(?![A-Za-z0-9_-])"
	),
	re.compile(r"(?<![A-Za-z0-9_])NuclearCrisis(?![A-Za-z0-9_])"),
	re.compile(
		r"(?<![A-Za-z0-9_.-])com\.ra2mac\.afghanistan-crisis\.game"
		r"(?![A-Za-z0-9_.-])"
	),
)
PATH_SPECIFIC_TECHNICAL_IDENTIFIERS = {
	"launcher.py": ('f"NUCLEAR-CRISIS-BG-{index:02d}.png"',),
	"packaging/generate_nuke_hour_icons.py": (
		'f"NUCLEAR-CRISIS-{suffix}"',
	),
}
RETIRED_VISIBLE_BRAND_PATTERNS = (
	re.compile(r"NUCLEAR[\s-]+CRISIS", re.IGNORECASE),
	re.compile(r"OpenRTS[\s-]+Mobile", re.IGNORECASE),
	re.compile(r"Afghan(?:istan)?[\s-]+Crisis", re.IGNORECASE),
	re.compile(r"(?:Red Alert 2[：:]\s*)?阿富汗危机"),
)


def read_source(relative_path: str) -> str:
	return (ROOT / relative_path).read_text(encoding="utf-8")


def shell_config_value(source: str, name: str) -> str:
	matches = re.findall(
		rf'^{re.escape(name)}="([^"]*)"$',
		source,
		re.MULTILINE,
	)
	if len(matches) != 1:
		raise AssertionError(f"expected one {name} assignment, found {matches!r}")
	return matches[0]


def active_tracked_text_sources():
	result = subprocess.run(
		["git", "ls-files", "-z"],
		cwd=ROOT,
		check=True,
		capture_output=True,
	)
	for raw_path in result.stdout.split(b"\0"):
		if not raw_path:
			continue
		relative_path = raw_path.decode("utf-8")
		path = Path(relative_path)
		if relative_path.startswith(HISTORICAL_PREFIXES):
			continue
		if any(part == "tests" or part.endswith(".Test") for part in path.parts):
			continue
		if relative_path in AUDIT_GUARDS:
			continue

		contents = (ROOT / relative_path).read_bytes()
		if b"\0" in contents:
			continue
		try:
			yield relative_path, contents.decode("utf-8")
		except UnicodeDecodeError:
			continue


def retired_visible_brand_matches(source: str, relative_path: str | None = None):
	masked_source = source
	for identifier in STABLE_TECHNICAL_IDENTIFIERS:
		masked_source = identifier.sub(
			lambda match: " " * len(match.group(0)),
			masked_source,
		)
	for identifier in PATH_SPECIFIC_TECHNICAL_IDENTIFIERS.get(
		relative_path, ()
	):
		masked_source = masked_source.replace(identifier, " " * len(identifier))

	return sorted(
		(
			match.start(),
			source[match.start() : match.end()],
		)
		for pattern in RETIRED_VISIBLE_BRAND_PATTERNS
		for match in pattern.finditer(masked_source)
	)


class CrossPlatformBrandingTest(unittest.TestCase):
	@classmethod
	def setUpClass(cls):
		cls.mod_config = read_source("mod.config")
		cls.windows_packager = read_source("packaging/windows/buildpackage.sh")
		cls.windows_installer = read_source("packaging/windows/buildpackage.nsi")
		cls.linux_packager = read_source("packaging/linux/buildpackage.sh")
		cls.macos_packager = read_source("packaging/macos/buildpackage.sh")
		cls.public_clean_script = read_source("ios/scripts/build-public-clean.sh")

	def test_windows_outputs_launcher_and_compatibility_key_are_stable(self):
		self.assertEqual(
			"NUKE-HOUR",
			shell_config_value(self.mod_config, "PACKAGING_INSTALLER_NAME"),
		)
		self.assertEqual(
			"NUKE-HOUR",
			shell_config_value(self.mod_config, "PACKAGING_WINDOWS_LAUNCHER_NAME"),
		)
		self.assertEqual(
			"OpenRARA2Mod",
			shell_config_value(self.mod_config, "PACKAGING_WINDOWS_REGISTRY_KEY"),
		)
		self.assertIn(
			'${OUTPUTDIR}/${PACKAGING_INSTALLER_NAME}-${TAG}-${PLATFORM}.exe',
			self.windows_packager,
		)
		self.assertIn(
			'${OUTPUTDIR}/${PACKAGING_INSTALLER_NAME}-${TAG}-${PLATFORM}-winportable.zip',
			self.windows_packager,
		)
		self.assertIn(
			'${BUILTDIR}/${PACKAGING_WINDOWS_LAUNCHER_NAME}.exe',
			self.windows_packager,
		)

	def test_windows_pe_and_shortcuts_use_the_visible_product_name(self):
		self.assertEqual(
			"NUKE HOUR",
			shell_config_value(self.mod_config, "PACKAGING_DISPLAY_NAME"),
		)
		for field in ("ProductName", "FileDescription"):
			with self.subTest(field=field):
				self.assertIn(
					f'--set-version-string "{field}" "${{PACKAGING_DISPLAY_NAME}}"',
					self.windows_packager,
				)

		self.assertIn(
			'!define MUI_STARTMENUPAGE_DEFAULTFOLDER "${PACKAGING_DISPLAY_NAME}"',
			self.windows_installer,
		)
		self.assertIn(
			'CreateShortCut "$SMPROGRAMS\\$StartMenuFolder\\${PACKAGING_DISPLAY_NAME}.lnk"',
			self.windows_installer,
		)
		self.assertIn(
			'CreateShortCut "$DESKTOP\\${PACKAGING_DISPLAY_NAME}.lnk"',
			self.windows_installer,
		)
		self.assertIn(
			'Delete "$DESKTOP\\${PACKAGING_DISPLAY_NAME}.lnk"',
			self.windows_installer,
		)
		self.assertNotIn("$DESKTOP\\OpenRA - ", self.windows_installer)

	def test_linux_output_is_branded_while_internal_launcher_stays_stable(self):
		self.assertEqual("ra2", shell_config_value(self.mod_config, "MOD_ID"))
		self.assertIn(
			'${OUTPUTDIR}/${PACKAGING_INSTALLER_NAME}-${TAG}-x86_64.AppImage',
			self.linux_packager,
		)
		self.assertIn(
			'${APPDIR}/usr/bin/openra-${MOD_ID}',
			self.linux_packager,
		)
		self.assertEqual(
			"openra-ra2",
			f"openra-{shell_config_value(self.mod_config, 'MOD_ID')}",
		)

	def test_macos_dmg_and_public_clean_ipa_outputs_are_branded(self):
		self.assertEqual(
			"NUKE HOUR",
			shell_config_value(self.mod_config, "PACKAGING_MACOS_DISPLAY_NAME"),
		)
		self.assertEqual(
			"NUKE-HOUR",
			shell_config_value(self.mod_config, "PACKAGING_MACOS_INSTALLER_NAME"),
		)
		self.assertIn(
			'${OUTPUTDIR}/${PACKAGING_MACOS_INSTALLER_NAME}-${TAG}.dmg',
			self.macos_packager,
		)
		self.assertIn(
			'PACKAGING_OSX_APP_NAME="${PACKAGING_MACOS_DISPLAY_NAME}.app"',
			self.macos_packager,
		)
		self.assertIn(
			'-volname "${PACKAGING_MACOS_DISPLAY_NAME}"',
			self.macos_packager,
		)
		self.assertIn(
			'IPA="$ARTIFACT_DIR/NUKE HOUR-PublicClean.ipa"',
			self.public_clean_script,
		)
		self.assertIn(
			'shasum -a 256 "$IPA" > "$ARTIFACT_DIR/NUKE HOUR-PublicClean.ipa.sha256"',
			self.public_clean_script,
		)

	def test_stable_host_bundle_and_wallpaper_identifiers_are_preserved(self):
		launcher = read_source("launcher.py")
		self.assertIn('"NuclearCrisis"', launcher)
		self.assertIn(
			'f"NUCLEAR-CRISIS-BG-{index:02d}.png" for index in range(1, 10)',
			launcher,
		)
		self.assertIn(
			"com.ra2mac.afghanistan-crisis.game",
			read_source("NUKE HOUR GAME.app/Contents/Info.plist"),
		)
		for index in range(1, 10):
			with self.subTest(index=index):
				wallpaper = (
					ROOT
					/ "mods"
					/ "ra2"
					/ "uibits"
					/ f"NUCLEAR-CRISIS-BG-{index:02d}.png"
				)
				self.assertTrue(
					wallpaper.is_file(),
					f"missing stable wallpaper identifier: {wallpaper}",
				)

	def test_old_brand_guard_has_only_narrow_technical_exceptions(self):
		allowed = (
			"NUCLEAR-CRISIS-BG-01.png",
			"NUCLEAR-CRISIS-BG-09.png",
			"NuclearCrisis",
			"com.ra2mac.afghanistan-crisis.game",
		)
		for value in allowed:
			with self.subTest(allowed=value):
				self.assertEqual([], retired_visible_brand_matches(value))

		blocked = (
			"NUCLEAR CRISIS",
			"NUCLEAR-CRISIS",
			"NUCLEAR CRISIS.app",
			"NUCLEAR CRISIS GAME.app",
			"NUCLEAR CRISIS.command",
			"NUCLEAR-CRISIS.icns",
			"NUCLEAR-CRISIS.dmg",
			"NUCLEAR-CRISIS installer",
			"NUCLEAR-CRISIS-BG-99.png",
			"XNUCLEAR-CRISIS-BG-06.pngY",
			"OpenRTS Mobile",
			"Afghanistan Crisis",
			"Afghanistan-Crisis",
			"Red Alert 2：阿富汗危机",
		)
		for value in blocked:
			with self.subTest(blocked=value):
				self.assertNotEqual([], retired_visible_brand_matches(value))

		path_specific = PATH_SPECIFIC_TECHNICAL_IDENTIFIERS
		for relative_path, identifiers in path_specific.items():
			for identifier in identifiers:
				with self.subTest(relative_path=relative_path, identifier=identifier):
					self.assertEqual(
						[],
						retired_visible_brand_matches(identifier, relative_path),
					)
					self.assertNotEqual(
						[],
						retired_visible_brand_matches(identifier, "other.py"),
					)

	def test_active_source_has_no_retired_visible_brand(self):
		violations = []
		for relative_path, source in active_tracked_text_sources():
			for start, match in retired_visible_brand_matches(source, relative_path):
				line = source.count("\n", 0, start) + 1
				violations.append(f"{relative_path}:{line}: {match!r}")

		self.assertEqual([], violations, "\n".join(violations))


if __name__ == "__main__":
	unittest.main()
