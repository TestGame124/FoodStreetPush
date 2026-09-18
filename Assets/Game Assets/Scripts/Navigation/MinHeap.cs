namespace FoodStreet.Navigation
{
    /// <summary>
    /// Binary min-heap of (cell, priority) pairs, backed by a growable array.
    ///
    /// There is no decrease-key. A cell whose score improves is simply pushed again and
    /// the stale entry is skipped when it pops, which costs a little memory and saves the
    /// sibling index bookkeeping that decrease-key needs. The search closes cells on pop,
    /// so a duplicate can never be expanded twice.
    /// </summary>
    public sealed class MinHeap
    {
        int[] cells;
        int[] priorities;
        int count;

        public MinHeap(int capacity = 256)
        {
            if (capacity < 4) capacity = 4;
            cells = new int[capacity];
            priorities = new int[capacity];
        }

        public int Count => count;

        public void Clear() => count = 0;

        public void Push(int cell, int priority)
        {
            if (count == cells.Length) Grow();

            int i = count++;
            cells[i] = cell;
            priorities[i] = priority;

            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (priorities[parent] <= priorities[i]) break;
                Swap(parent, i);
                i = parent;
            }
        }

        public int Pop(out int priority)
        {
            int cell = cells[0];
            priority = priorities[0];

            count--;
            if (count > 0)
            {
                cells[0] = cells[count];
                priorities[0] = priorities[count];
                SiftDown(0);
            }

            return cell;
        }

        void SiftDown(int i)
        {
            while (true)
            {
                int left = (i << 1) + 1;
                if (left >= count) return;

                int smallest = left;
                int right = left + 1;
                if (right < count && priorities[right] < priorities[left]) smallest = right;

                if (priorities[i] <= priorities[smallest]) return;

                Swap(i, smallest);
                i = smallest;
            }
        }

        void Swap(int a, int b)
        {
            (cells[a], cells[b]) = (cells[b], cells[a]);
            (priorities[a], priorities[b]) = (priorities[b], priorities[a]);
        }

        void Grow()
        {
            int size = cells.Length * 2;
            System.Array.Resize(ref cells, size);
            System.Array.Resize(ref priorities, size);
        }
    }
}
