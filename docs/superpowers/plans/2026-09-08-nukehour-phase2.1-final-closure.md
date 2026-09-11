# NUKE HOUR Phase 2.1 Final Evidence Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to execute this plan task-by-task.

**Goal:** Close the remaining Phase 2.1 evidence gates without changing the multiplayer protocol, then produce the redacted evidence JSON and final report.

**Architecture:** Reuse the existing public endpoint, physical Apple smoke harness, controlled compatibility fixtures, existing audited artifacts, and local regression scripts. Evidence is written only with redacted public-address fields and records the actual server lifecycle (including clean empty-room shutdown).

**Tech Stack:** Shell, Python, xcrun devicectl, SSH/Workbench, existing OpenRA smoke scripts, JSON/Markdown evidence.

---

### Task 1: Verify public same-session stability and host independence

**Files:**
- Read: `/tmp/nukehour-public4567-phase2.1/*`
- Read: remote service journal/status via SSH

- [ ] Confirm both Apple journals share one run/session, map, runtime contract, and endpoint; require >=1800 ticks, no desync, no order rejection, and no unexpected disconnect.
- [ ] Confirm one client can finish/leave while the other remains connected and the server remains alive until all clients leave.
- [ ] Record the server transition to clean empty-room shutdown without changing lifecycle policy.

### Task 2: Run public negative compatibility admission

**Files:**
- Read: existing controlled protocol/runtime mismatch fixture and client harness
- Create: temporary evidence under `/tmp/nukehour-public4567-negative`

- [ ] Use an existing mismatch fixture against the public endpoint; require an explicit protocol/runtime-contract rejection and no crash or timeout.
- [ ] Do not modify retail resources or public artifacts.

### Task 3: Verify x86_64 artifact integrity and resource audit

**Files:**
- Read: `/Users/kevin/Projects/ra2-mac/artifacts/server/NUKE-HOUR-Server-1.0.3-phase2-final-linux-x64.tar.gz`
- Create: `docs/testing/results/dedicated-server-phase2.1/evidence.json`

- [ ] Compare local SHA256 with a remote SHA256 of the deployed x86_64 artifact.
- [ ] Confirm remote architecture, version, commit, READY/listen state, valid admission, and clean shutdown.
- [ ] Audit deployed contents for forbidden commercial assets and record `Commercial assets included = NONE`.

### Task 4: Re-run Apple Local Multiplayer regression

**Files:**
- Create: temporary evidence under `/tmp/nukehour-local-regression-phase2.1`

- [ ] Run iOS Host → macOS Client and macOS Host → iOS Client over LAN Direct IP on TCP 1234.
- [ ] Require Connect, Handshake, Lobby, Start, Gameplay, no desync, and no order rejection.

### Task 5: Generate redacted final evidence and report

**Files:**
- Create: `docs/testing/results/dedicated-server-phase2.1/evidence.json`
- Create: `docs/nukehour-multiplayer-phase2.1-final-report.md`

- [ ] Include same-session, host independence, empty lifecycle, public negative, x86_64, ARM64, local regression, artifact integrity, and commercial-audit results.
- [ ] Redact the public address as `8.135.x.x`; record TCP 4567 only.
- [ ] Record Android physical validation as PENDING and Phase 1 as PARTIAL.

### Task 6: Verify, commit, and stop

- [ ] Run repository tests/audits relevant to changed evidence and report files.
- [ ] Review the exact diff and complete staged file list.
- [ ] Commit only the new plan/evidence/report files; do not stage unrelated user artifacts.
- [ ] Stop after Phase 2.1 closure; do not begin Lobby, matchmaking, accounts, orchestration, or autoscaling.
