# Controller test: managed plugin 0.3.4

The prototype uses the exact native renderer binary from the working 0.2.0 headset test. It adds a right-controller ray and trigger selection through the game's existing interactable callbacks. No manual extraction is needed for this session.

1. Connect Quest 3 through Steam Link and start SteamVR, then launch Inscryption through Epic.
2. Enter an Act 1 or Kaycee's Mod battle. Use desktop input for menus. Dialogue should appear in a headset subtitle panel; press and release right trigger to advance an input message. Wait until it is your turn, then press F8. The seat uses a consistent table anchor.
3. Release the right trigger first. Aim the right controller at a card. A ray should appear and turn green over an eligible target.
4. Press and release the right trigger to select. Begin with a card, then try an eligible board slot and the bell. Report whether the ray appears, highlighting works, and selection happens once.
5. **F7** toggles the 10-degree viewing tilt. **F10** toggles controller input and restores mouse control. **F8** recenters the view. **F9** stops VR rendering.

6. On the map, the raised overview should appear automatically and persist until you leave the map. **T** selects full top-down; **S** restores seated for the current map/non-map context.
7. Try a reward card and a sacrifice altar's card slots/confirmation. Only those two event families have new support; other special events still need an audit. Release the trigger after transitions before selecting again.

Timing diagnostics are read-only. Rotate your head gently for about 30 seconds so the logs capture several samples of the remaining distortion.

The pointer suspends during pause, transitions and unsupported gameplay phases. Maps have preliminary support; story events, cabin walking, menus, direct card grabbing and haptics are unfinished. Controller tracking loss cancels the current press, and a held trigger must be released before selection resumes.

Compilation passed, alongside 25 trigger-state assertions, 12 OpenVR ABI checks, 23 staged stick-gesture assertions, 34 installed-game metadata checks and 49 native baseline mock assertions. These checks use no live headset interaction. Pointer alignment, game callback behavior and compatibility still require this test. The agent can read the game logs directly afterward.

The 0.3.2 battle pointer and seating were confirmed by a complete user-played fight. The 0.3.3 dialogue, subtitle and tilt additions remain unverified on the headset. Their checks add 25 context-routing assertions and raise metadata coverage to 38 members; both production binding constructors pass. See [runtime test 07](runtime-test-07.md).

The production adapter constructor also passes an execution check against the installed assemblies. This new check reproduces the 0.3.1 visibility-flags bug that stopped input initialization. See [runtime test 06](runtime-test-06.md).
