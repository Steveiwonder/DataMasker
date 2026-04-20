using DataMasker.Utils;

namespace DataMasker.Tests
{
    [TestClass]
    public class BatchTests
    {
        [TestMethod]
        public void BatchNo_StartsAtOne()
        {
            var batches = Batch<int>.BatchItems(new[] { 1, 2, 3 }, (_, items) => items.Count() < 10).ToList();
            Assert.AreEqual(1, batches[0].BatchNo);
        }

        [TestMethod]
        public void BatchItems_EmptyInput_ReturnsOneBatch()
        {
            var batches = Batch<int>.BatchItems(Array.Empty<int>(), (_, items) => true).ToList();
            Assert.AreEqual(1, batches.Count);
            Assert.AreEqual(0, batches[0].Items.Count);
        }

        [TestMethod]
        public void BatchItems_AllInOneBatch_WhenPredicateAlwaysTrue()
        {
            var items = new[] { 1, 2, 3, 4, 5 };
            var batches = Batch<int>.BatchItems(items, (_, existing) => true).ToList();
            Assert.AreEqual(1, batches.Count);
            Assert.AreEqual(5, batches[0].Items.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, batches[0].Items.ToArray());
        }

        [TestMethod]
        public void BatchItems_SplitsCorrectly_WhenBatchSizeTwo()
        {
            var items = new[] { 1, 2, 3, 4, 5 };
            var batches = Batch<int>.BatchItems(items, (_, existing) => existing.Count() < 2).ToList();
            // [1,2], [3,4], [5]
            Assert.AreEqual(3, batches.Count);
            Assert.AreEqual(2, batches[0].Items.Count);
            Assert.AreEqual(2, batches[1].Items.Count);
            Assert.AreEqual(1, batches[2].Items.Count);
        }

        [TestMethod]
        public void BatchItems_BatchNumbers_IncrementForEachBatch()
        {
            var items = new[] { 10, 20, 30, 40 };
            var batches = Batch<int>.BatchItems(items, (_, existing) => existing.Count() < 1).ToList();
            for (int i = 0; i < batches.Count; i++)
                Assert.AreEqual(i + 1, batches[i].BatchNo);
        }

        [TestMethod]
        public void BatchItems_SingleItem_ReturnsOneBatchWithOneItem()
        {
            var batches = Batch<string>.BatchItems(new[] { "only" }, (_, _) => true).ToList();
            Assert.AreEqual(1, batches.Count);
            Assert.AreEqual(1, batches[0].Items.Count);
            Assert.AreEqual("only", batches[0].Items[0]);
        }

        [TestMethod]
        public void AddItem_IncreasesItemCount()
        {
            var batches = Batch<string>.BatchItems(new[] { "a", "b", "c" }, (_, _) => true).ToList();
            Assert.AreEqual(3, batches[0].Items.Count);
        }

        [TestMethod]
        public void BatchItems_PredicateAlwaysFalse_YieldsEmptyFirstBatchThenOnePerItem()
        {
            var items = new[] { 1, 2, 3 };
            var batches = Batch<int>.BatchItems(items, (_, existing) => false).ToList();
            // First batch is empty (yielded before first item is added), then one item per batch
            Assert.AreEqual(4, batches.Count);
            Assert.AreEqual(0, batches[0].Items.Count);
            Assert.AreEqual(1, batches[1].Items.Count);
            Assert.AreEqual(1, batches[2].Items.Count);
            Assert.AreEqual(1, batches[3].Items.Count);
        }
    }
}
