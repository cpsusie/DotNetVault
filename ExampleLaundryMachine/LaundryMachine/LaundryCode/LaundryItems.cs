using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public readonly struct LaundryItems : IEquatable<LaundryItems>, IComparable<LaundryItems>
    {
        #region Static Property and Factory Method
        public static ref readonly LaundryItems InvalidItem => ref TheInvalidItem;

        public static LaundryItems CreateLaundryItems([NotNull] string description, decimal weightKg, byte soiledFactor, byte dampnessFactor)
        {
            if (description == null) throw new ArgumentNullException(nameof(description));
            if (weightKg <= 0) throw new ArgumentOutOfRangeException(nameof(weightKg), weightKg, "Parameter must be positive.");
            return new LaundryItems(Guid.NewGuid(), description, weightKg, soiledFactor, dampnessFactor);
        }
        #endregion

        #region Public Properties
        public Guid ItemId { get; }
        [NotNull] public string ItemDescription => _itemDescription ?? string.Empty;
        public decimal TotalWeightKg { get; }
        public byte SoiledFactor { get; }
        public byte Dampness { get; } 
        #endregion

        #region IEquatable / IComparable
        public static bool operator ==(in LaundryItems lhs, in LaundryItems rhs) => lhs.ItemId == rhs.ItemId;
        public static bool operator !=(in LaundryItems lhs, in LaundryItems rhs) => !(lhs == rhs);
        public static bool operator >(in LaundryItems lhs, in LaundryItems rhs) => Compare(in lhs, in rhs) > 0;
        public static bool operator <(in LaundryItems lhs, in LaundryItems rhs) => Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in LaundryItems lhs, in LaundryItems rhs) => !(lhs < rhs);
        public static bool operator <=(in LaundryItems lhs, in LaundryItems rhs) => !(lhs > rhs);
        public override int GetHashCode() => ItemId.GetHashCode();
        public override bool Equals(object other) => (other as LaundryItems?) == this;
        public bool Equals(LaundryItems other) => other == this;
        public int CompareTo(LaundryItems other) => Compare(in this, in other);
        #endregion
        
        #region Public Methods
        public override string ToString() =>
            $"LaundryItems.  Description: {ItemDescription}, Weight: {TotalWeightKg:F3} kg, Soiled: " +
            $"{(SoiledFactor / 255.0) * 100.0}%, Damp: {(Dampness / 255.0) * 100.0}%";
        
        [Pure]
        public LaundryItems WithWeightDampnessAndSoiled(decimal weightKg, byte dampness, byte soiled)
        {
            if (weightKg <= 0)
                throw new ArgumentOutOfRangeException(nameof(weightKg), weightKg, "Parameter must be positive.");
            return new LaundryItems(ItemId, ItemDescription, weightKg, dampness, soiled);
        }
        [Pure]
        public LaundryItems WithSoilFactor(byte newSoilFactor) => this == InvalidItem
            ? this
            : new LaundryItems(ItemId, ItemDescription, TotalWeightKg, newSoilFactor, Dampness);
        [Pure]
        public LaundryItems WithDampness(byte newDampness) =>
            this == InvalidItem 
                ? this
                : new LaundryItems(ItemId, ItemDescription, TotalWeightKg, SoiledFactor, newDampness);

        #endregion

        #region Private CTOR
        private LaundryItems(Guid id, [NotNull] string description, decimal totalWeight, byte soiledFactor, byte dampness)
        {
            ItemId = id;
            _itemDescription = description;
            TotalWeightKg = totalWeight;
            SoiledFactor = soiledFactor;
            Dampness = dampness;
        } 
        #endregion

        #region Private Method
        private static int Compare(in LaundryItems lhs, in LaundryItems rhs) => lhs.ItemId.CompareTo(rhs.ItemId); 
        #endregion

        #region PrivateData
        private readonly string _itemDescription;
        private static readonly LaundryItems TheInvalidItem = default; 
        #endregion

        
    }
}