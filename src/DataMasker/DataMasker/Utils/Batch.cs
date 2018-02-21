using System;
using System.Collections.Generic;
using System.Linq;

namespace DataMasker.Utils
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Batch<T>
    {
        public int BatchNo { get; }

        public IList<T> Items { get; }

        private Batch(
            int batchNo)
        {
            BatchNo = batchNo;
            Items = new List<T>();
        }

        /// <summary>
        /// Adds the item.
        /// </summary>
        /// <param name="item">The item.</param>
        public void AddItem(
            T item)
        {
            Items.Add(item);
        }


        /// <summary>
        /// This method with batch an array of items base on a predicate
        /// </summary>
        /// <param name="items">The starting array of objects to batch</param>
        /// <param name="addToCurrentBatchPredicate">
        /// A callback function which will determine if the curren item should be put on the current batch or next?
        /// The call back will receive the current item and the items in the current batch, the predicate should return true if the
        /// current item should be added to the current batch
        /// </param>
        /// <returns></returns>
        public static IEnumerable<Batch<T>> BatchItems(
            T[] items,
            Func<T, IEnumerable<T>, bool> addToCurrentBatchPredicate)
        {
            int currentBatchNo = 1;
            List<Batch<T>> batchedItems = new List<Batch<T>>();
            Batch<T> currentBatch = new Batch<T>(currentBatchNo);

            for (int i = 0; i < items.Length; i++)
            {
                T item = items[i];
                bool addToCurrentBatch = addToCurrentBatchPredicate(item, currentBatch.Items);

                if (addToCurrentBatch)
                {
                    currentBatch.AddItem(item);
                }
                else
                {
                    batchedItems.Add(currentBatch);
                    currentBatch = new Batch<T>(++currentBatchNo);
                    currentBatch.AddItem(item);
                }
            }

            batchedItems.Add(currentBatch);
            return batchedItems;
        }
    }
}
