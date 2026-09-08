# Build 4 Secret and Signing-Material Audit

## Result

```text
PRIVATE_KEY_FILES=0
PRIVATE_KEY_BLOCKS=0
KNOWN_TOKEN_SIGNATURES=0
PRIVATE_PROVISIONING_PROFILES_IN_SOURCE=0
SOURCE_SECRET_AUDIT=PASS
```

The final scan of the deny-by-default public projection inspected 5,045
regular files. It found zero suspicious secret/signing filenames and zero
high-confidence private-key, AWS, GitHub, OpenAI, Google, Slack, Stripe-live,
or assigned-secret signatures.

Generated `bin`, `obj`, `artifacts`, and `DerivedData` directories were excluded. The signed Build 4 IPA contains a signature and embedded provisioning profile, but neither is source and neither was extracted into this worktree.

Checks covered suspicious filenames; private-key/PKCS#12 headers; common API-key, token, client-secret, password-assignment, GitHub, AWS, OpenAI, Google, and Slack signatures; and signing/provision references.

No secret values or private signing files were found. Password hits in server launch scripts, packaging scripts, and translation patches are variable names, options, or UI text. Signing references are public configuration metadata or placeholders; historical private certificate/profile material is absent.

`.gitignore` now excludes `artifacts/`, `DerivedData/`, AOT data, private profiles, PKCS#12 containers, PEM/key files, and local `private/`/`signing/` directories, in addition to existing `bin`, `obj`, and user support-data exclusions.

This gate passes for source. It does not authorize publishing the signed IPA or its embedded profile.
