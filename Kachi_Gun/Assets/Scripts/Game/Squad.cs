using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KachiGun
{
    /// <summary>
    /// 분대(리더 1 + 동료 2). 장착 칸 0 = 리더.
    /// 스테이지 = 정해진 자리(Field.Home + 동료 자리)에 고정, 터치해도 안 움직임.
    /// 던전(도전·광산·보스) = 가장 가까운 대상 쪽으로 이동 + 화면을 누르고 있으면 리더가 그쪽으로 (기획서 2.1).
    /// </summary>
    public class Squad : MonoBehaviour
    {
        public static Squad I;
        public readonly List<Capy> cats = new();
        public Capy Leader => cats.Count > 0 ? cats[0] : null;
        public bool Manual { get; private set; }
        public Vector2 manualTarget;
        float manualHold;
        Transform marker;

        void Awake() { I = this; }

        public void Rebuild()
        {
            var s = SaveData.Cur;
            Vector2 lp = Leader ? (Vector2)Leader.transform.position : Field.I.Home;
            foreach (var c in cats) Destroy(c.gameObject);
            cats.Clear();
            int n = 0;
            for (int i = 0; i < SaveData.SlotCount; i++)
            {
                if (s.slots[i] < 0) continue;
                var c = Capy.Make(i, transform);
                c.SetWeapon(s.slots[i]);
                c.transform.position = lp + Offset(n);
                cats.Add(c);
                n++;
            }
        }

        public void SetSkin(int skin) { foreach (var c in cats) c.SetSkin(skin); }

        Vector2 Offset(int i) => i == 0 ? Vector2.zero : i == 1 ? Field.I.Sub1Offset : Field.I.Sub2Offset;

        public void ResetPosition()
        {
            for (int i = 0; i < cats.Count; i++) cats[i].transform.position = Field.I.Home + Offset(i);
            Manual = false;
        }

        void Update()
        {
            if (cats.Count == 0) return;
            float dt = Time.deltaTime;
            var gm = GameManager.I;

            // 스테이지: 자리 고정
            if (gm.mode == GameManager.Mode.Stage)
            {
                Manual = false; blocked = false;
                for (int i = 0; i < cats.Count; i++)
                {
                    cats[i].transform.position = Field.I.Home + Offset(i);
                    cats[i].moving = false;
                }
                if (marker) marker.gameObject.SetActive(false);
                return;
            }

            ReadInput();
            var leader = Leader;
            Vector2 lp = leader.transform.position;
            Vector2 goal = lp;

            if (Manual) goal = manualTarget;
            else
            {
                // 던전 AI: 가장 가까운 대상에게 사거리 70% 까지 다가감 (광산에선 가치 구분 없이 가까운 것)
                var t = Target.Nearest(lp, 99f);
                if (t != null)
                {
                    float r = leader.weapon != null ? leader.weapon.range * 0.7f : 4f;
                    Vector2 d = t.Pos - lp;
                    if (d.magnitude > r) goal = lp + d.normalized * (d.magnitude - r);
                }
            }

            goal = Field.I.Clamp(goal);
            float spd = Tables.P("Move_Speed", Tables.Cf("MoveSpeed"));
            Vector2 np = Vector2.MoveTowards(lp, goal, spd * dt);
            leader.moving = (np - lp).magnitude > 0.001f;
            leader.transform.position = np;

            for (int i = 1; i < cats.Count; i++)
            {
                var c = cats[i];
                Vector2 want = Field.I.Clamp(np + Offset(i), 0.3f);
                Vector2 cp = c.transform.position;
                Vector2 nc = Vector2.MoveTowards(cp, want, spd * 1.15f * dt);
                c.moving = (nc - cp).magnitude > 0.001f;
                c.transform.position = nc;
            }

            if (marker) marker.gameObject.SetActive(Manual);
        }

        void ReadInput()
        {
            bool down = false;
            Vector2 sp = default;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began && OverUi(t.fingerId)) { blocked = true; }
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { blocked = false; }
                else if (!blocked) { down = true; sp = t.position; }
            }
            else if (Input.GetMouseButton(0))
            {
                if (Input.GetMouseButtonDown(0) && OverUi(-1)) blocked = true;
                if (!blocked) { down = true; sp = Input.mousePosition; }
            }
            else blocked = false;

            if (down && GameManager.I.InputAllowed)
            {
                Vector2 w = Camera.main.ScreenToWorldPoint(sp);
                manualTarget = Field.I.Clamp(w);
                Manual = true;
                manualHold = 1.5f;     // 손을 떼고 1.5초 지나면 AI 로 돌아감
                ShowMarker(manualTarget);
            }
            else if (Manual)
            {
                manualHold -= Time.unscaledDeltaTime;
                if (manualHold <= 0 || (Leader && ((Vector2)Leader.transform.position - manualTarget).magnitude < 0.1f && manualHold < 1.2f))
                    Manual = false;
            }
        }
        bool blocked;

        static bool OverUi(int id)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return id >= 0 ? es.IsPointerOverGameObject(id) : es.IsPointerOverGameObject();
        }

        void ShowMarker(Vector2 p)
        {
            if (!marker)
            {
                var go = new GameObject("MoveMarker");
                var s = go.AddComponent<SpriteRenderer>();
                s.sprite = Art.Get("Bullet/crosshair", 220f);
                s.color = new Color(1f, 1f, 1f, 0.6f);
                s.sortingOrder = -20000;
                marker = go.transform;
            }
            marker.position = p;
            marker.Rotate(0, 0, 90f * Time.unscaledDeltaTime);
        }

        /// <summary>시험·광고 연출용: 강제로 수동 이동</summary>
        public void ForceMove(Vector2 p) { manualTarget = Field.I.Clamp(p); Manual = true; manualHold = 3f; }
    }
}
