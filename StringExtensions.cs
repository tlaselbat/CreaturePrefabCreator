namespace CreaturePrefabCreator
{
    public static class StringExtensions
    {
        /// <summary>
        /// Computes a stable hash code consistent with Valheim's internal hashing.
        /// </summary>
        public static int GetStableHashCode(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;

            int num = 0;
            foreach (char c in str)
            {
                num = ((num << 5) + num) ^ c;
            }
            return num;
        }
    }
}
