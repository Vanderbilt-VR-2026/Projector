using System;
using Projector.Networking;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Projector.Gameplay
{
    // Who a networked player object belongs to: slot (set by the server) and display name (set by the owner).
    // Tints `tinted` renderers in the slot color and fills `nameLabel`.
    public class PlayerIdentity : NetworkBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>();
        public readonly NetworkVariable<FixedString64Bytes> DisplayName = new NetworkVariable<FixedString64Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [SerializeField] Renderer[] tinted;
        [SerializeField] Text nameLabel;

        public event Action Changed;

        public string Name => DisplayName.Value.IsEmpty ? $"Player {Slot.Value + 1}" : DisplayName.Value.ToString();
        public Color Color => PlayerColors.Get(Slot.Value);

        public override void OnNetworkSpawn()
        {
            Slot.OnValueChanged += OnSlotChanged;
            DisplayName.OnValueChanged += OnNameChanged;
            if (IsOwner)
                DisplayName.Value = SessionService.LocalPlayerName;
            Apply();
        }

        public override void OnNetworkDespawn()
        {
            Slot.OnValueChanged -= OnSlotChanged;
            DisplayName.OnValueChanged -= OnNameChanged;
        }

        void OnSlotChanged(int previous, int current) => Apply();

        void OnNameChanged(FixedString64Bytes previous, FixedString64Bytes current) => Apply();

        void Apply()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, Color);
            foreach (var target in tinted)
                target.SetPropertyBlock(block);

            if (nameLabel != null)
            {
                nameLabel.text = Name;
                nameLabel.color = Color;
            }

            Changed?.Invoke();
        }
    }
}
