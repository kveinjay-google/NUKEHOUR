# NUKE HOUR — Archived iOS Build 4 Source

This repository preserves the source corresponding to NukeHour iOS Build 4,
built on September 5, 2026. It is a historical source archive, not the current
development version.

The archive contains the modified OpenRA/RA2 source, iOS host, AOT factory
generator and build integration, build files, and the documentation needed to
inspect or reproduce an unsigned build. It intentionally does **not** contain
the Build 4 IPA, original Red Alert 2 or Yuri's Revenge assets, user-imported
game data, signing certificates, private keys, provisioning profiles, or
credentials. Users must supply eligible game data from their own lawful
installation at runtime.

Start with:

- [Building the archival source](BUILDING.md)
- [Build 4 release metadata](docs/releases/IOS_BUILD_4.md)
- [Archival source notice](NOTICE.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)
- [Build 4 source mapping](docs/archive/BUILD4_SOURCE_MAPPING.md)

This is an independent community project. Electronic Arts has not endorsed and
does not support it. OpenRA project notices and licensing remain in the source
tree.
-------------------------------------------------------------------------------------
A note on authenticity

While I was developing and sharing this project, some people claimed that the videos were AI-generated, fake, or that the project did not actually exist.

I was genuinely disappointed by that.

Being skeptical of a project before its source is available is completely reasonable. But calling real development work "AI-generated" or "fake" without evidence is something very different.

I don't want to prove this project is real by arguing about it.

The historical source corresponding to iOS Build 4 is now public. It has been checked against the original PDB source records, rebuilt as an iOS Release build, and its AOT factory registry was regenerated from the recovered source. The regenerated registry matches the original Build 4 version byte-for-byte.

If you're interested in whether the project is real, how it works, or what was actually changed, you can now inspect the source, read the build documentation, and try building it yourself.

Technical criticism, code review, and disagreement are always welcome.

From this point on, I'd rather let the source code and reproducible engineering evidence answer those questions.
