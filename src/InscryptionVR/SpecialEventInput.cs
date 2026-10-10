using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace InscryptionVR
{
    // Called only while Part1's game state is SpecialCardSequence. Ownership matters:
    // other events reuse the same views and must not acquire this pointer accidentally.
    internal sealed class SpecialEventInput
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Type _handlerType;
        private readonly FieldInfo _reward, _merger, _rewardCards, _reroll;
        private readonly FieldInfo _sacrifice, _host, _confirm, _stones, _selector, _displayedCards;
        private readonly PropertyInfo _slotCard, _enabled;
        private Component _handler;

        internal SpecialEventInput()
        {
            Assembly game = null;
            foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
                if (candidate.GetType("DiskCardGame.SpecialNodeHandler", false) != null) { game = candidate; break; }
            if (game == null) throw new InvalidOperationException("Inscryption event assembly not loaded");
            _handlerType = game.GetType("DiskCardGame.SpecialNodeHandler", true);
            _reward = Field(_handlerType, "cardChoiceSequencer");
            _merger = Field(_handlerType, "cardMerger");
            _rewardCards = Field(game.GetType("DiskCardGame.CardChoicesSequencer", true), "selectableCards");
            _reroll = Field(_reward.FieldType, "rerollInteractable");
            _sacrifice = Field(_merger.FieldType, "sacrificeSlot");
            _host = Field(_merger.FieldType, "hostSlot");
            _confirm = Field(_merger.FieldType, "confirmStone");
            _stones = Field(_merger.FieldType, "stoneCircleAnim");
            _selector = Field(_sacrifice.FieldType, "cardSelector");
            _displayedCards = Field(_selector.FieldType, "displayedCards");
            _slotCard = _sacrifice.FieldType.GetProperty("Card", Members);
            _enabled = game.GetType("DiskCardGame.InteractableBase", true).GetProperty("Enabled", Members);
            if (_slotCard == null || _enabled == null) throw new MissingMemberException("Inscryption event interaction contract changed");
            // Resolve metadata only. Finding/creating Unity objects here breaks the host-side check.
        }

        internal bool IsAvailable(int view)
        {
            if (view != 2 && view != 3 && view != 8 && view != 9) return false;
            if (!Resolve()) return false;
            if (view == 3)
            {
                Component reward = _reward.GetValue(_handler) as Component;
                return Active(reward) && (AnyLive(_rewardCards.GetValue(reward)) || Live(_reroll.GetValue(reward) as Component));
            }
            Component merger = ActiveMerger();
            if (merger == null) return false;
            Component sacrifice = _sacrifice.GetValue(merger) as Component;
            Component host = _host.GetValue(merger) as Component;
            if (view == 2) return AnyLive(CardsInSelector(sacrifice)) || AnyLive(CardsInSelector(host));
            return Live(sacrifice) || Live(host) || Live(SlotCard(sacrifice)) || Live(SlotCard(host)) ||
                Live(_confirm.GetValue(merger) as Component);
        }

        internal bool AllowsTarget(Component target, int view)
        {
            if (!Live(target) || !IsAvailable(view)) return false;
            if (view == 3)
            {
                Component reward = _reward.GetValue(_handler) as Component;
                return Contains(_rewardCards.GetValue(reward), target) || target == (_reroll.GetValue(reward) as Component);
            }
            Component merger = ActiveMerger();
            if (merger == null) return false;
            Component sacrifice = _sacrifice.GetValue(merger) as Component;
            Component host = _host.GetValue(merger) as Component;
            if (view == 2) return Contains(CardsInSelector(sacrifice), target) || Contains(CardsInSelector(host), target);
            return target == sacrifice || target == host || target == SlotCard(sacrifice) || target == SlotCard(host) ||
                target == (_confirm.GetValue(merger) as Component);
        }

        private bool Resolve()
        {
            if (!Active(_handler)) _handler = UnityEngine.Object.FindObjectOfType(_handlerType) as Component;
            return Active(_handler);
        }

        private Component ActiveMerger()
        {
            Component merger = _merger.GetValue(_handler) as Component;
            // MergeSequence shows this visual before revealing slots, and hides it on exit.
            // Sequencer components themselves coexist with unrelated special events.
            return Active(merger) && Active(_stones.GetValue(merger) as Component) ? merger : null;
        }

        private object CardsInSelector(Component slot)
        {
            if (!Active(slot)) return null;
            Component selector = _selector.GetValue(slot) as Component;
            return Active(selector) ? _displayedCards.GetValue(selector) : null;
        }

        private Component SlotCard(Component slot)
        {
            return slot == null ? null : _slotCard.GetValue(slot, null) as Component;
        }

        private bool AnyLive(object collection)
        {
            IEnumerable items = collection as IEnumerable;
            if (items != null) foreach (object item in items) if (Live(item as Component)) return true;
            return false;
        }

        private static bool Contains(object collection, Component target)
        {
            IEnumerable items = collection as IEnumerable;
            if (items != null) foreach (object item in items) if ((item as Component) == target) return true;
            return false;
        }

        private bool Live(Component component) { return Active(component) && (bool)_enabled.GetValue(component, null); }
        private static bool Active(Component component) { return component != null && component.gameObject.activeInHierarchy; }
        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, Members);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            return field;
        }
    }
}
