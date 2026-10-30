// WARNING: Do not modify! Generated file.
// 10-30 빈 키 (결제 꺼짐) — 새 개발자 계정의 라이선스 키는 메뉴 카치건 > 결제 라이선스 키 넣기 로 넣으면 이 파일이 다시 만들어짐

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("");
        private static int[] order = new int[] {  };
        private static int key = 0;

        public static readonly bool IsPopulated = false;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
