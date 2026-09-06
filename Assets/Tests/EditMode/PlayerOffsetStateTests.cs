using NUnit.Framework;
using PocketBlaster.Gameplay;
using UnityEngine;

namespace PocketBlaster.Tests.EditMode
{
    public class PlayerOffsetStateTests
    {
        [Test]
        public void StartsAtZero()
        {
            var state = new PlayerOffsetState(2.5f);
            Assert.AreEqual(Vector3.zero, state.Offset);
        }

        [Test]
        public void StepMovesInFlattenedDirection()
        {
            var state = new PlayerOffsetState(10f);
            // 上向き成分(y)を含む方向を渡しても、水平(xz)方向だけに動くべき
            var offset = state.Step(new Vector3(0f, 5f, 1f), 1f);
            Assert.AreEqual(0f, offset.y, 0.0001f);
            Assert.Greater(offset.z, 0f);
            Assert.AreEqual(0f, offset.x, 0.0001f);
        }

        [Test]
        public void RepeatedStepsAccumulate()
        {
            var state = new PlayerOffsetState(10f);
            state.Step(Vector3.forward, 1f);
            var offset = state.Step(Vector3.forward, 1f);
            Assert.AreEqual(2f, offset.magnitude, 0.001f);
        }

        [Test]
        public void OffsetClampsToMaxRadius()
        {
            var state = new PlayerOffsetState(2f);
            for (var i = 0; i < 10; i++)
            {
                state.Step(Vector3.forward, 1f);
            }
            Assert.AreEqual(2f, state.Offset.magnitude, 0.001f);
        }

        [Test]
        public void ZeroDirectionIsIgnored()
        {
            var state = new PlayerOffsetState(5f);
            state.Step(Vector3.forward, 1f);
            var before = state.Offset;
            var after = state.Step(Vector3.zero, 1f);
            Assert.AreEqual(before, after);
        }

        [Test]
        public void ResetReturnsToZero()
        {
            var state = new PlayerOffsetState(5f);
            state.Step(Vector3.forward, 1f);
            state.Reset();
            Assert.AreEqual(Vector3.zero, state.Offset);
        }

        [Test]
        public void ComputeStepResultDoesNotMutateState()
        {
            var state = new PlayerOffsetState(10f);
            var previewed = state.ComputeStepResult(Vector3.forward, 1f);
            Assert.Greater(previewed.magnitude, 0f);
            Assert.AreEqual(Vector3.zero, state.Offset, "ComputeStepResultは内部状態を変えないこと");
        }

        [Test]
        public void ComputeStepResultMatchesSubsequentStep()
        {
            var state = new PlayerOffsetState(10f);
            var previewed = state.ComputeStepResult(Vector3.forward, 1f);
            var stepped = state.Step(Vector3.forward, 1f);
            Assert.AreEqual(previewed, stepped, "試算と実際のStepは同じ結果になること");
        }

        [Test]
        public void SetOffsetOverridesCurrentOffsetDirectly()
        {
            var state = new PlayerOffsetState(10f);
            state.Step(Vector3.forward, 1f);
            var target = new Vector3(3f, 0f, 4f);
            state.SetOffset(target);
            Assert.AreEqual(target, state.Offset);
        }
    }
}
