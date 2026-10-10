# Card reward and sacrifice altar controls

The event pointer supports ordinary card rewards and the sacrifice altar while Act 1 is in `SpecialCardSequence`. It identifies eligible targets from the installed game's exact sequencer fields rather than enabling every event that happens to share a camera view.

For rewards, `Choices` view accepts live cards in `SpecialNodeHandler.cardChoiceSequencer.selectableCards` and its live reroll interactable. For the altar, `CardMergeSlots` and `CardMergeConfirm` accept the owning sacrifice/host slots, cards placed on those slots, and the confirmation stone. In `DeckSelection`, only live displayed cards owned by those slots' card selectors qualify. The altar's stone-circle visual must be active as additional evidence that its event is present.

The usual target collider/Enabled checks and global interaction restrictions still apply. View locks are not click locks: the altar deliberately locks camera navigation while allowing slot selection. Trigger press invokes the game's slot/card callbacks; release confirms through the normal confirmation callback. Animation-time disabled targets remain unavailable.

Other events, deck-review navigation, and altar cancellation are not included in this prototype. Runtime validation requires choosing a reward, choosing both altar cards, and confirming the altar in the headset. Constructor validation only verifies that reflection metadata resolves against the installed assemblies; it cannot prove scene object ownership or collider behavior.
