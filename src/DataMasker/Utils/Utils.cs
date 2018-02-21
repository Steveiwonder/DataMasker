using Bogus.DataSets;

namespace DataMasker.Utils
{
    public static class Utils
    {
        /// <summary>
        /// Tries to parse gender.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static Name.Gender? TryParseGender(
            string value)
        {
            if (value == null)
            {
                return null;
            }

            value = value.ToLower();

            if (value == "m" ||
                value == "male")
            {
                return Name.Gender.Male;
            }

            if (value == "f" ||
                value == "female")
            {
                return Name.Gender.Female;
            }

            return null;
        }
    }
}