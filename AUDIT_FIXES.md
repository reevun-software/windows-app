# Audit fixes — 2026-10-04

Addresses the Windows half of F19: override http-cache-semantics to ^4.3.0 and update package-lock without changing Electron or the installer/release workflow.

Validation: npm ci --ignore-scripts, tsc --noEmit --noUnusedLocals --noUnusedParameters, npm audit (0 findings). The shared app-core was previously built successfully; its code is unchanged. No installer was published and no installed GUI/autoupdate test was performed. Check a packaged installer in staging before merging: this repository's main-branch workflow publishes the release automatically.

Baseline commit: `e71c7cc0ae29c2ac6a078970a345077f1d71b4a9`. Work branch: `audit-fixes-2026-10-04`. No merge or production deployment has been performed.
