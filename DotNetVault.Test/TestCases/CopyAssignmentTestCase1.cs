using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using Rwlkres = DotNetVault.LockedResources.RwLockedResource<DotNetVault.Vaults.BasicReadWriteVault<DotNetVault.Test.TestCases.MutableStructExample>, DotNetVault.Test.TestCases.MutableStructExample>;
namespace DotNetVault.Test.TestCases
{
    static class CopyAssignmentTests
    {
        public static void ReadWriteAccess()
        {
            Point3d newPoint = new Point3d(-19.121m, 0.2m, 199.96m);

            //FLAW: SHOULD NOT BE ABLE TO DO THIS .. .actually may not be a problem
            //can't call dispose on copy and it has scope lesser in extent to the scope of lck.
            {
                using Rwlkres lck = TheRwVault.Lock();
                var copy = lck; //Figure out why this isn't being flagged as an error
                using Rwlkres copy2 = copy;
                using (var cop3 = copy2)
                {
                    Console.WriteLine(cop3.Value.ToString());
                    Console.WriteLine(@"The value is: [" + lck.Value + @"].");
                    lck.Value.UpdatePoint(in newPoint);
                    Console.WriteLine(@"The value is: [" + lck.Value + @"].");
                }
            }
            
            //static void DoStuff(Rwlkres foo)
            //{
            //    Console.WriteLine(foo.Value.ToString());
            //}
        }

       

        private static readonly BasicReadWriteVault<MutableStructExample> TheRwVault =
            new BasicReadWriteVault<MutableStructExample>(MutableStructExample.CreateExample("Steve"),
                TimeSpan.FromMilliseconds(250));
    }

    [VaultSafe]
    public struct MutableStructExample : IEquatable<MutableStructExample>
    {
        public static ref readonly MutableStructExample InvalidDefault => ref TheInvalidDefault;

        public static MutableStructExample CreateExample([JetBrains.Annotations.NotNull] string name) =>
            new MutableStructExample(default, DateTime.Now, Guid.NewGuid(), name);

        //Note that getters that are NOT auto implemented must be MARKED readonly to avoid defensive 
        //copying.
        [JetBrains.Annotations.NotNull]
        public string Name
        {
            readonly get => _name ?? string.Empty;
            set => _name = value ?? throw new ArgumentNullException(nameof(value));
        }

        public Guid Id
        {
            readonly get => _id;
            set => _id = value;
        }

        public DateTime TimeStamp
        {
            readonly get => _stamp;
            set => _stamp = value;
        }

        public decimal X
        {
            readonly get => _point3d.X;
            set => _point3d.X = value;
        }

        public decimal Y
        {
            readonly get => _point3d.Y;
            set => _point3d.Y = value;
        }

        public decimal Z
        {
            readonly get => _point3d.Z;
            set => _point3d.Z = value;
        }

        public void UpdatePoint(in Point3d p) => _point3d = p;

        public static bool operator ==(in MutableStructExample lhs, in MutableStructExample rhs)
            => lhs._id == rhs._id && lhs._stamp == rhs._stamp && lhs._point3d == rhs._point3d;
        public static bool operator !=(in MutableStructExample lhs, in MutableStructExample rhs) => !(lhs == rhs);

        // NOTE the only time this is a problem for a VALUE type is when it serves as 
        // the hash function in for a set or dictionary based on hash codes.  It
        //can only be a problem if you can access the mutators of this value by
        //MUTABLE REFERENCE.  Otherwise calling a mutator on a key read from a dictionary or 
        //set will merely change the COPY not the value stored in the dictionary if it is returned
        //by value or will be ineffective if it is returned by reference because it will affect a temporary
        //and immediately discarded defensive copy.  There is no reason to fear this for vault safe type
        //unless someone comes up with a hash set or other ordered collection that foolishly exposes
        //such values by mutable reference.
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public readonly override int GetHashCode() => _id.GetHashCode();
        public readonly override bool Equals(object other) => other is MutableStructExample mse && mse == this;
        public bool Equals(MutableStructExample other) => other == this;
        public override string ToString() =>
            "Id: [" + _id + "]; Stamp: [" + _stamp.ToString("O") + "]; Point: [" + _point3d + "].";

        private MutableStructExample(in Point3d point, DateTime ts, Guid id,
            [JetBrains.Annotations.NotNull] string name)
        {
            _point3d = point;
            _id = id;
            _stamp = ts;
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }


        private Point3d _point3d;
        private DateTime _stamp;
        private Guid _id;
        private string _name;
        private static readonly MutableStructExample TheInvalidDefault = default;
    }

    public struct Point3d : IEquatable<Point3d>
    {
        //Note that auto implemented getters are implicitly readonly.
        public decimal X { get; set; }

        public decimal Y { get; set; }

        public decimal Z { get; set; }

        public Point3d(decimal x, decimal y, decimal z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static bool operator ==(in Point3d lhs, in Point3d rhs) =>
            lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;
        public static bool operator !=(in Point3d lhs, in Point3d rhs) => !(lhs == rhs);
        public override readonly bool Equals(object other) => other is Point3d p3d && p3d == this;
        public readonly bool Equals(Point3d other) => other == this;

        public override readonly string ToString() => string.Format("X: [{0:F3}]; Y: [{1:F3}]; Z: [{2:F3}].", X, Y, Z);

        // NOTE the only time this is a problem for a VALUE type is when it serves as 
        // the hash function in for a set or dictionary based on hash codes.  It
        //can only be a problem if you can access the mutators of this value by
        //MUTABLE REFERENCE.  Otherwise calling a mutator on a key read from a dictionary or 
        //set will merely change the COPY not the value stored in the dictionary if it is returned
        //by value or will be ineffective if it is returned by reference because it will affect a temporary
        //and immediately discarded defensive copy.  There is no reason to fear this for vault safe type
        //unless someone comes up with a hash set or other ordered collection that foolishly exposes
        //such values by mutable reference.
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override readonly int GetHashCode()
        {
            int hash = X.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
            }
            return hash;
        }
    }
}
