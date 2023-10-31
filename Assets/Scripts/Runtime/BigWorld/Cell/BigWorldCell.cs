namespace BigCat.BigWorld
{
    public class BigWorldCell
    {
        protected readonly int m_x;
        public int x => m_x;

        protected readonly int m_z;
        public int z => m_z;

        public int index => BigWorldUtility.GetCellIndex(m_x, m_z);

        protected BigWorldCell(int index)
        {
            BigWorldUtility.GetCellCoordinates(index, out m_x, out m_z);
        }
    }
}