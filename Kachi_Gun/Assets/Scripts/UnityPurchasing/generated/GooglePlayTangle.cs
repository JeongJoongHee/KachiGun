// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("ComHiLgKiYKKComJiAF0Ayvos7g4wIsGJMHJ2IcChaAXlAAKmIt8iTbRVcqPiBDtSU0nngDnw8gZSc519uPSc+0PaGZSLRZBbsUgIBXzSSr1SUWH7/0Q1WfBdTdP7utOVGJI1HY1FOg/xQ+Z0bHXyMKXxhl6xt4JmRxtnjlTcCotQhWYmsXkkrDV1lqBq1B/i0eU08fNmDBDmWDRffZIMMjz+WITqCUWM2LQ5GTWiq9cExZPuAqJqriFjoGiDsAOf4WJiYmNiIs8O0vPI1fGC5Ov7AaBP+ckXUQFhQ8D8UnZ/60xP1w2kZ1j1FoFyZXAl3L5riEn7is+I8f0EPjX6RPH1BhIyF8Uz/LVflnpvNGNNMqXFUX4nzFS6kBVDwWakYqLiYiJ");
        private static int[] order = new int[] { 1,3,2,5,4,7,13,11,13,11,10,11,12,13,14 };
        private static int key = 136;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
