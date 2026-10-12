using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimeSim
{
    public enum CharacterSlot { Hair, Top, Bottom, Shoes }

    /// <summary>
    /// Bibliothek austauschbarer Teile pro Slot. Ist für einen Slot/Index ein Prefab hinterlegt,
    /// wird es statt (Hair) bzw. zusätzlich zu (Kleidung) den Platzhalter-Primitives genutzt.
    /// Prefabs können IBodyShapeTarget implementieren, um Fett/Muskel per Blendshape umzusetzen.
    /// </summary>
    [CreateAssetMenu(menuName = "CrimeSim/Character Part Library", fileName = "CharacterPartLibrary")]
    public class CharacterPartLibrary : ScriptableObject
    {
        [Serializable]
        public class SlotEntry
        {
            public CharacterSlot slot;
            public List<GameObject> prefabs = new List<GameObject>();
        }

        public List<SlotEntry> slots = new List<SlotEntry>();

        public int Count(CharacterSlot slot)
        {
            foreach (var e in slots)
                if (e != null && e.slot == slot && e.prefabs != null) return e.prefabs.Count;
            return 0;
        }

        public GameObject Get(CharacterSlot slot, int index)
        {
            foreach (var e in slots)
            {
                if (e == null || e.slot != slot || e.prefabs == null) continue;
                if (index >= 0 && index < e.prefabs.Count) return e.prefabs[index];
            }
            return null;
        }
    }
}
