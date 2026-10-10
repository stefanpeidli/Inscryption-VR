# Runtime test 04: 0.2.1 regression and rollback

The user reported that 0.2.1 barely displayed a usable view and then crashed. This invalidates the predicted-before-render refinement for the current game integration, despite its passing isolated mock tests.

The run's BepInEx and Unity logs, configuration and failed plugin files are preserved under `artifacts/runtime-test-04-crash`. The logs confirm 0.2.1 initialization and at least one accepted stereo frame, then end without a managed exception or acknowledged shutdown. They do not establish the native crash cause. The normal loader debug messages skipping native DLLs as .NET assemblies are expected and are not evidence of this failure.

The agent restored both managed and native DLLs plus configuration from `artifacts/installed-backups/20261009-032516-0022`. This is the user-verified 0.2.0 renderer. Each installed file was compared to its backup by hash, and the installed managed assembly reports version 0.2.0.0. Built-artifact staging was also restored to the same baseline. The installer rejects assembly version 0.2.1.0 to prevent accidental redeployment.

No game assemblies or save files were edited by the rollback. The prior timing change is withdrawn; it must not be presented as a fix for the original minor warping. Native mock tests covered ABI and resource ownership but did not validate Unity's live graphics-thread/compositor synchronization. That integration remains a suspect, not a confirmed cause.

For the subsequent 0.3.0 controller prototype, native source was restored to the baseline timing sequence and the exact verified 0.2.0 native binary was retained. The withdrawn timing experiment's mock fixture remains available for investigation; its passing tests did not establish live rendering safety.
