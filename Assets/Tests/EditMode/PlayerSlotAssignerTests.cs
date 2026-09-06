using NUnit.Framework;
using PocketBlaster.Networking;

namespace PocketBlaster.Tests.EditMode
{
    public class PlayerSlotAssignerTests
    {
        [Test]
        public void AssignsSlotsInOrderStartingFromZero()
        {
            var assigner = new PlayerSlotAssigner(2);
            Assert.AreEqual(0, assigner.Assign(connectionId: 100));
            Assert.AreEqual(1, assigner.Assign(connectionId: 200));
        }

        [Test]
        public void ReturnsNullWhenFull()
        {
            var assigner = new PlayerSlotAssigner(2);
            assigner.Assign(100);
            assigner.Assign(200);
            Assert.IsNull(assigner.Assign(300));
        }

        [Test]
        public void ReassigningSameConnectionIdReturnsExistingSlot()
        {
            var assigner = new PlayerSlotAssigner(2);
            var first = assigner.Assign(100);
            var second = assigner.Assign(100);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void ReleasedSlotCanBeReassignedToANewConnection()
        {
            var assigner = new PlayerSlotAssigner(2);
            assigner.Assign(100);
            assigner.Assign(200);
            assigner.Release(100);

            var reassigned = assigner.Assign(300);
            Assert.AreEqual(0, reassigned, "解放されたスロット0が再割当されるべき");
        }

        [Test]
        public void ReleaseOfUnknownConnectionIdIsNoOp()
        {
            var assigner = new PlayerSlotAssigner(2);
            assigner.Assign(100);
            assigner.Release(999);
            Assert.AreEqual(1, assigner.Assign(200), "無関係なReleaseの影響を受けていないこと");
        }

        [Test]
        public void TryGetSlotReflectsCurrentAssignment()
        {
            var assigner = new PlayerSlotAssigner(2);
            assigner.Assign(100);

            Assert.IsTrue(assigner.TryGetSlot(100, out var slot));
            Assert.AreEqual(0, slot);

            Assert.IsFalse(assigner.TryGetSlot(999, out _));
        }
    }
}
