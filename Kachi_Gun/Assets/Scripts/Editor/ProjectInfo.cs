using UnityEditor;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 앱 이름 (10-20 결정: "카피바라vs좀비"). 에디터를 열거나 스크립트가 다시 컴파일될 때 Player Settings 의 Product Name 을 맞춘다.
    /// 이름을 바꾸려면 Player Settings 가 아니라 여기 Name 을 고칠 것 (여기 값으로 다시 돌아감).
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectInfo
    {
        public const string Name = "카피바라vs좀비";

        static ProjectInfo()
        {
            if (PlayerSettings.productName != Name) PlayerSettings.productName = Name;
        }
    }
}
