﻿using System.Text;
using Google.ProtocolBuffers;
using MHServerEmu.Common.Extensions;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.GameData;

namespace MHServerEmu.Games.Entities.Locomotion
{
    public class LocomotionStateUpdateArchive
    {
        private const int LocFlagCount = 16;

        public uint ReplicationPolicy { get; set; }
        public ulong EntityId { get; set; }
        public bool[] LocFlags { get; set; }
        public PrototypeId PrototypeId { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Orientation { get; set; }
        public LocomotionState LocomotionState { get; set; }

        public LocomotionStateUpdateArchive(ByteString data)
        {
            CodedInputStream stream = CodedInputStream.CreateInstance(data.ToByteArray());

            ReplicationPolicy = stream.ReadRawVarint32();
            EntityId = stream.ReadRawVarint64();
            LocFlags = stream.ReadRawVarint32().ToBoolArray(LocFlagCount);
            if (LocFlags[11]) PrototypeId = stream.ReadPrototypeEnum(PrototypeEnumType.Entity);
            Position = new(stream, 3);
            if (LocFlags[0])
                Orientation = new(stream, 6);
            else
                Orientation = new(stream.ReadRawZigZagFloat(6), 0f, 0f);
            LocomotionState = new(stream, LocFlags);
        }

        public LocomotionStateUpdateArchive() { }

        public ByteString Serialize()
        {
            using (MemoryStream ms = new())
            {
                CodedOutputStream cos = CodedOutputStream.CreateInstance(ms);

                cos.WriteRawVarint32(ReplicationPolicy);
                cos.WriteRawVarint64(EntityId);
                cos.WriteRawVarint32(LocFlags.ToUInt32());
                if (LocFlags[11]) cos.WritePrototypeEnum(PrototypeId, PrototypeEnumType.Entity);
                Position.Encode(cos, 3);
                if (LocFlags[0])
                    Orientation.Encode(cos, 6);
                else
                    cos.WriteRawZigZagFloat(Orientation.X, 6);
                LocomotionState.Encode(cos, LocFlags);

                cos.Flush();
                return ByteString.CopyFrom(ms.ToArray());
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.AppendLine($"ReplicationPolicy: 0x{ReplicationPolicy:X}");
            sb.AppendLine($"EntityId: {EntityId}");

            sb.Append("LocFlags: ");
            for (int i = 0; i < LocFlags.Length; i++) if (LocFlags[i]) sb.Append($"{i} ");
            sb.AppendLine();

            sb.AppendLine($"PrototypeId: {GameDatabase.GetPrototypeName(PrototypeId)}");
            sb.AppendLine($"Position: {Position}");
            sb.AppendLine($"Orientation: {Orientation}");
            sb.AppendLine($"LocomotionState: {LocomotionState}");
            return sb.ToString();
        }
    }
}
