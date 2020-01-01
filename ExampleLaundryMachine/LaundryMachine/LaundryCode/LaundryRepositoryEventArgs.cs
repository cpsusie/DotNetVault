using System;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class LaundryRepositoryEventArgs : EventArgs
    {
        public Guid LaundryRepoId { get; }
        public DateTime TimeStamp { get; }
        [NotNull] public string LaundryItemDescription { get; }
        public Guid LaundryItemId { get; }
        public bool RemovedFromRepo => !AddedToRepo;
        public bool AddedToRepo { get; }

        public LaundryRepositoryEventArgs([NotNull] ILaundryRepository repo, in LaundryItems li, bool added)
        {
            if (repo == null) throw new ArgumentNullException(nameof(repo));
            TimeStamp = TimeStampSource.Now;
            AddedToRepo = added;
            LaundryRepoId = repo.Id;
            LaundryItemId = li.ItemId;
            LaundryItemDescription = li.ItemDescription;
        }

        public override string ToString() =>
            $"At {DateTime.Now:O}, Laundry with id: {LaundryItemId.ToString()} was {(AddedToRepo ? "added to" : "removed from")} the repository.";
    }
}