using System;
using System.Runtime.Serialization;

namespace Arcweave.Project
{
    public enum VariableScopeType
    {
        [EnumMember(Value = "global")]
        Global,

        [EnumMember(Value = "boards")]
        Boards
    }

    public static class VariableScopeTypeExtensions
    {
        public static VariableScopeType FromString(string value)
        {
            return value switch
            {
                "global" => VariableScopeType.Global,
                "boards" => VariableScopeType.Boards,
                _ => throw new ArgumentException($"Unknown value: {value}", nameof(value))
            };
        }

        public static string ToStringValue(this VariableScopeType type)
        {
            return type switch
            {
                VariableScopeType.Global => "global",
                VariableScopeType.Boards => "boards",
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }
    }
}