using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KachiGun
{
    /// <summary>
    /// 게임 진행: 스테이지(N-1~N-5, N-5 끝에 변이 오우거 보스) · 도전(60초 섬멸) · 다이아 광산(고정 피해 1) · 보스 던전.
    /// 총알 뽑기/합성·스킨·미션 규칙도 여기서 처리하고, 화면은 HUD 가 그린다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum Mode { Stage, Challenge, Mine, Boss }
        public static GameManager I;
        public Mode mode = Mode.Stage;
        public event Action Changed;          // 골드·레벨 등 화면 갱신
        public event Action StageChanged;

        static SaveData S => SaveData.Cur;
        float spawnT, saveT, dailyT;
        public Zombie boss;
        public bool bossPhase;
        public float bossTime;            // 보스 남은 시간
        public bool upgradeHint;          // 보스 실패 뒤 강화 안내 화살표 (강화하면 꺼짐)
        public bool BossFarm => S.bossFarm;
        public float BossTimeMax { get { float t = Tables.Stage(S.stage).bossTime; return t > 0 ? t : Tables.Cf("BossTime"); } }

        // 던전
        public float dungeonTime;
        public double dungeonGems;
        public int dungeonChapter, dungeonTotal;
        bool dungeonOver;
        public bool eco;

        public bool CanShoot => !dungeonOver && !Demo && !HoldFire;
        public static bool Demo;   // 시험 촬영(PlayTest.RunZombie): 좀비 생성·사격 멈춤
        public static bool HoldFire;   // 스토어 촬영(PlayTest.RunStore, 11-07): 좀비가 화면 안으로 걸어 들어올 때까지 사격만 멈춤 — 게임에선 늘 false
        public bool InputAllowed => !eco && !(HUD.I && HUD.I.PopupOpen);

        void Awake() { I = this; Tables.Load(); }

        void Start()
        {
            S.RefreshDaily();
            S.ClampTimers();
            ApplyTint();
            Squad.I.Rebuild();
            Squad.I.ResetPosition();
            StageChanged += AudioManager.RefreshBgm;
            StageChanged?.Invoke();
            StartCoroutine(OfflineAtStart());
        }

        public void Notify()
        {
            // 11-13 산 총알이 인벤토리가 가득 차 기다리던 중이면, 합성·분해·장착으로 빈 칸이 생기는 순간 넣음
            if (S.pendingBullets.Count > 0 && S.DeliverPending()) { S.SaveSoon(); HUD.I?.Toast(L.T("기다리던 총알이 인벤토리에 들어갔습니다")); }
            Changed?.Invoke();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            saveT += Time.unscaledDeltaTime;
            if (saveT > 5f) { saveT = 0; S.Save(false); }   // 11-08: 자동 저장은 메모리에만 (저장소 쓰기는 30초마다 · 나갈 때 바로)
            dailyT += Time.unscaledDeltaTime;
            if (dailyT > 10f) { dailyT = 0; S.RefreshDaily(); S.ClampTimers(); CheckAttend(false); }   // 켜 둔 채 날이 바뀜 → 창이 없을 때 출석 창

            switch (mode)
            {
                case Mode.Stage: StageTick(dt); AutoUpgradeTick(); break;
                default: DungeonTick(dt); break;
            }
        }

        // =====================================================================
        // 스테이지
        // =====================================================================
        // 11-05 몰려오기: 일반 스테이지 좀비 수 = 표 값 x Horde_Mul (처치 목표·동시 최대·나오는 속도),
        // 좀비 하나 체력·골드는 ÷ Horde_Mul → 스테이지 전체 체력·골드는 그대로. Horde_Pack_Min~Max 마리씩 무리로 나옴.
        // 보스전 동안은 예전대로(표 값 그대로 한 마리씩) — 졸개가 많아져 보스가 어려워지지 않게.
        public static float HordeMul => Mathf.Max(1f, (float)Tables.C("Horde_Mul", 1));
        public int Quota { get { int q = Tables.Stage(S.stage).quota; if (q <= 0) q = Tables.Ci("KillsPerStage"); return Mathf.Max(1, Mathf.RoundToInt(q * HordeMul)); } }
        public float StageProgress => bossPhase ? Mathf.Clamp01(bossTime / BossTimeMax) : S.bossFarm ? 1f : Mathf.Clamp01(S.kills / (float)Quota);

        // 11-08: 무리(6~10마리)를 한 프레임에 다 만들면 그 프레임이 무거움 → 줄을 세워 한 프레임에 2마리씩 (오른쪽 화면 밖이라 보이는 건 같음)
        readonly Queue<(Vector2 p, float hm)> spawnQueue = new();
        const int SpawnPerFrame = 2;

        void StageTick(float dt)
        {
            for (int i = 0; i < SpawnPerFrame && spawnQueue.Count > 0; i++) { var q = spawnQueue.Dequeue(); SpawnZombie(q.p, q.hm); }
            int alive = spawnQueue.Count;   // 줄 선 좀비도 있는 셈
            foreach (var t in Target.All) if (t is Zombie z && z.Alive && !z.isBoss) alive++;

            var st = Tables.Stage(S.stage);
            if (!bossPhase && !S.bossFarm && S.kills >= Quota)
            {
                if (st.boss) StartBoss();
                else { AdvanceStage(); return; }
            }

            // 보스 제한 시간: 못 잡으면 보스가 물러나고 반복 사냥
            if (bossPhase)
            {
                bossTime -= dt;
                if (bossTime <= 0) { BossFail(); return; }
            }

            spawnT -= dt;
            if (spawnT > 0 || Demo) return;
            if (bossPhase)
            {
                if (alive < st.maxAlive) { spawnT = st.spawn * Random.Range(0.7f, 1.3f); SpawnZombie(Field.I.SpawnPoint(), 1f); }
                return;
            }
            float hm = HordeMul;
            int room = Mathf.RoundToInt(st.maxAlive * hm) - alive;
            int left = S.bossFarm ? int.MaxValue : Quota - S.kills - alive;
            int pMin = Mathf.Max(1, Tables.Ci("Horde_Pack_Min")), pMax = Mathf.Max(pMin, Tables.Ci("Horde_Pack_Max"));
            int need = Mathf.Min(pMin, left);       // 자리가 무리 하나만큼 빌 때까지 기다림 (한 마리씩 흘러나오지 않게)
            if (need <= 0 || room < need) return;
            int n = Mathf.Min(Random.Range(pMin, pMax + 1), Mathf.Min(room, left));
            SpawnPack(n, hm);
            spawnT = st.spawn / hm * n * Random.Range(0.8f, 1.2f);   // 평균 나오는 속도 = 표 속도 x Horde_Mul
        }

        /// <summary>무리 (11-05 2차): 흙 띠 높이를 n 칸으로 나눠 칸마다 한 마리씩(칸 안에서 조금 흔듦) → 한곳에 쏠리지 않고 위아래 고르게.
        /// 앞뒤는 0 ~ Horde_Pack_Depth 칸 사이로 흩어져 줄줄이 들이닥침. 좀비는 나온 높이 그대로 걸어옴.</summary>
        void SpawnPack(int n, float hm)
        {
            float y0 = Field.I.SpawnPoint(-999f).y, y1 = Field.I.SpawnPoint(999f).y;
            float depth = Tables.C("Horde_Pack_Depth", 2.0) > 0 ? Tables.Cf("Horde_Pack_Depth") : 2f;
            var lanes = new List<int>();
            for (int i = 0; i < n; i++) lanes.Add(i);
            for (int i = n - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (lanes[i], lanes[j]) = (lanes[j], lanes[i]); }
            float h = (y1 - y0) / n;
            for (int i = 0; i < n; i++)
            {
                float y = y0 + h * (lanes[i] + 0.5f + Random.Range(-0.4f, 0.4f));
                var p = Field.I.SpawnPoint(y) + new Vector2(Random.Range(0f, depth), 0);
                spawnQueue.Enqueue((p, hm));
            }
        }

        void SpawnZombie(Vector2 p, float hm)
        {
            var r = PickZombie(S.Chapter, S.stage);
            Zombie.Spawn(r, p, Stats.ZombieHp(S.stage) * r.hpMul / hm, Stats.ZombieGold(S.stage) * r.goldMul / hm, tint: PickTint(S.Chapter, false));
        }

        /// <summary>11-11: 나올 비중 = 표 Base_Weight x 배경이 맞으면 Zombie_Biome_Mul x 처음 나온 뒤 Zombie_New_Chapters 챕터 동안 Zombie_New_Mul
        /// (새 얼굴 비중은 MinChapter 가 Zombie_New_From 이상인 좀비만 — 처음 5챕터의 원래 좀비 섞임은 그대로)</summary>
        public static float ZombieWeight(ZombieRow z, int chapter, string biome)
        {
            if (z.minChapter > chapter || z.weight <= 0) return 0;
            float w = z.weight;
            if (!string.IsNullOrEmpty(z.biome) && z.biome == biome) w *= (float)Tables.C("Zombie_Biome_Mul", 1);
            if (z.minChapter >= Tables.C("Zombie_New_From", 6) && chapter - z.minChapter < Tables.C("Zombie_New_Chapters", 0)) w *= (float)Tables.C("Zombie_New_Mul", 1);
            return w;
        }

        /// <summary>11-11 한 스테이지에 나오는 일반 좀비 = Stage_Kinds_Min ~ Max 종 (너무 정신없지 않게).
        /// 스테이지 번호(key)로 정해서 다시 와도 같음 · 그 챕터에 처음 나오는 좀비는 꼭 넣고 나머지는 나올 비중대로 뽑음 ·
        /// 돈가방(Role 보물)은 Treasure_Outside_Kinds = 1 이면 종류 수에 안 세고 원래 확률대로 가끔 끼어듦</summary>
        public static List<ZombieRow> StageKinds(int chapter, int key)
        {
            long ck = (long)key * 1000 + chapter;
            if (kindsCache.TryGetValue(ck, out var got)) return got;
            string biome = Tables.Chapter(chapter).biome;
            bool treasureOut = Tables.C("Treasure_Outside_Kinds", 1) > 0;
            var pool = new List<ZombieRow>();
            foreach (var z in Tables.Zombies)
                if (ZombieWeight(z, chapter, biome) > 0 && !(treasureOut && z.role == "보물")) pool.Add(z);
            var rng = new System.Random(key * 7919 + chapter * 31 + 17);
            int lo = Mathf.Max(1, (int)Tables.C("Stage_Kinds_Min", 3)), hi = Mathf.Max(lo, (int)Tables.C("Stage_Kinds_Max", 5));
            int n = Mathf.Min(pool.Count, lo + rng.Next(hi - lo + 1));
            var pick = new List<ZombieRow>();
            // 이 챕터가 첫 등장인 새 좀비 = 꼭
            for (int i = pool.Count - 1; i >= 0 && pick.Count < n; i--)
                if (pool[i].minChapter == chapter && chapter >= Tables.C("Zombie_New_From", 6)) { pick.Add(pool[i]); pool.RemoveAt(i); }
            while (pick.Count < n && pool.Count > 0)
            {
                float sum = 0;
                foreach (var z in pool) sum += ZombieWeight(z, chapter, biome);
                float k = (float)rng.NextDouble() * sum;
                int at = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++) { k -= ZombieWeight(pool[i], chapter, biome); if (k <= 0) { at = i; break; } }
                pick.Add(pool[at]); pool.RemoveAt(at);
            }
            if (kindsCache.Count > 64) kindsCache.Clear();
            kindsCache[ck] = pick;
            return pick;
        }
        static readonly Dictionary<long, List<ZombieRow>> kindsCache = new Dictionary<long, List<ZombieRow>>();

        /// <summary>스테이지(key)의 3~5종 안에서 나올 비중대로 한 마리. 돈가방은 그 챕터 전체 비중 중 제 몫(원래 확률) 그대로</summary>
        public static ZombieRow PickZombie(int chapter, int key)
        {
            string biome = Tables.Chapter(chapter).biome;
            if (Tables.C("Treasure_Outside_Kinds", 1) > 0)
            {
                float all = 0, tre = 0; ZombieRow tz = null;
                foreach (var z in Tables.Zombies)
                {
                    float w = ZombieWeight(z, chapter, biome);
                    all += w;
                    if (w > 0 && z.role == "보물") { tre += w; if (tz == null || Random.value < w / tre) tz = z; }
                }
                if (tz != null && Random.value * all < tre) return tz;
            }
            var kinds = StageKinds(chapter, key);
            float sum = 0;
            foreach (var z in kinds) sum += ZombieWeight(z, chapter, biome);
            float r = Random.value * sum;
            foreach (var z in kinds)
            {
                r -= ZombieWeight(z, chapter, biome);
                if (r <= 0) return z;
            }
            return kinds.Count > 0 ? kinds[kinds.Count - 1] : Tables.Zombies[0];
        }

        /// <summary>11-11 색 변형: 챕터 색(Chapter_Summary Zombie_Tint) — 일반 좀비는 Tint_Chance 확률, 보스는 늘 (0 = 원래 초록)</summary>
        public static int PickTint(int chapter, bool boss)
        {
            int k = Art.TintIndex(Tables.Chapter(chapter).zombieTint);
            if (k == 0) return 0;
            return boss || Random.value < Tables.C("Tint_Chance", 1) ? k : 0;
        }

        /// <summary>보스 실패: 보스는 오른쪽으로 물러남(보상 없음) → 지금 스테이지 반복 사냥, [보스 도전] 버튼</summary>
        public void BossFail()
        {
            if (!bossPhase) return;
            bossPhase = false;
            if (boss) boss.Retreat();
            boss = null;
            S.bossFarm = true;
            upgradeHint = true;
            S.Save();
            AudioManager.Play(AudioManager.Sfx.Fail);
            HUD.I?.Banner(L.T("보스 실패! 강화 후 다시 도전하세요"), new Color(1f, 0.55f, 0.45f));
            StageChanged?.Invoke();
            Notify();
        }

        /// <summary>[보스 도전] 버튼</summary>
        public string ChallengeBoss()
        {
            if (mode != Mode.Stage || bossPhase) return null;
            if (!S.bossFarm) return L.T("아직 보스를 만날 때가 아닙니다");
            S.bossFarm = false;
            StartBoss();
            return null;
        }

        void StartBoss()
        {
            bossPhase = true;
            bossTime = BossTimeMax;
            var st = Tables.Stage(S.stage);
            var r = Tables.Zombie(string.IsNullOrEmpty(st.bossId) || st.bossId == "-" ? "ogre" : st.bossId);
            var p = Field.I.SpawnPoint();
            double hp = st.bossHp > 0 ? st.bossHp : st.hp * Tables.C("BossHpMul");
            double gold = st.bossGold > 0 ? st.bossGold : st.gold * Tables.C("BossGoldMul");
            boss = Zombie.Spawn(r, p, hp, gold, st.bossScale > 0 ? st.bossScale : Tables.Cf("BossScale"), true, PickTint(S.Chapter, true));
            AudioManager.Play(AudioManager.Sfx.BossAppear);
            HUD.I?.Banner(L.T("보스 등장!"), new Color(1f, 0.4f, 0.3f));
            Speech.Say("Boss");
            StageChanged?.Invoke();
        }

        public void AdvanceStage()
        {
            int before = S.stage;
            S.stage++;
            S.kills = 0;
            S.bossFarm = false;
            upgradeHint = false;
            bossPhase = false;
            boss = null;
            S.statStages++;
            S.bestEver = Math.Max(S.bestEver, S.stage);
            bool newChapter = S.stage / 5 != before / 5;
            if (newChapter) ApplyTint();
            string biome = L.T(Tables.Chapter(S.Chapter).biome);
            HUD.I?.Banner("Stage " + SaveData.StageName(S.stage) + (newChapter && biome.Length > 0 ? "\n<size=34>" + biome + "</size>" : ""), Color.white);
            Speech.Say("Clear");      // 11-11 요청_04 "스테이지 클리어!" 말풍선
            S.Save();
            StageChanged?.Invoke();
            Notify();
        }

        // 11-11 해금 알림(글 + 버튼 빛)은 HUD.CheckUnlockFx 가 버튼이 열리는 순간을 보고 띄움 (다이아로 여는 총알 메뉴도 같이)

        public void ApplyTint()
        {
            Color c = mode switch
            {
                Mode.Mine => new Color(0.55f, 0.62f, 0.85f),
                Mode.Challenge => new Color(1f, 0.82f, 0.8f),
                Mode.Boss => new Color(0.8f, 0.68f, 0.68f),
                _ => Field.ChapterTint(S.Chapter),
            };
            Field.I.SetMap(Tables.Chapter(S.Chapter).mapSprite);   // 11-11 챕터 좀비 색에 맞춘 배경 판 (세모 나무 · 죽은 나무 · 황폐)
            Field.I.Tint(c);
        }

        // =====================================================================
        // 피해·처치
        // =====================================================================
        /// <summary>bossMul = 쏜 카피바라의 보스 피해 배율 (보급품 + 슬롯 강화)</summary>
        public void Damage(Target t, double dmg, Vector2 from, double bossMul = 1, bool knock = true)
        {
            if (mode == Mode.Mine) dmg = 1;      // 광산 = 고정 피해 1
            else if (t is Zombie z && z.isBoss) dmg *= bossMul;
            t.Hit(dmg, from, knock);
        }

        public void OnTargetDead(Target t)
        {
            if (t is Zombie z)
            {
                S.statKills++;
                if (mode == Mode.Stage)
                {
                    double g = z.gold * Stats.GoldMul;
                    Fx.I?.Money(z.Center, g, z.isBoss ? 5 : 2);
                    if (z.isBoss) { S.statBossKills++; AdvanceStage(); return; }
                    if (!bossPhase && !S.bossFarm) S.kills++;   // 반복 사냥 중엔 골드만
                    // 11-07: 처치마다 StageChanged(화면 전체 다시 그림)를 부르지 않음 — 진행 막대는 HUD 가 0.1초마다 갱신
                }
                else if (mode == Mode.Challenge || mode == Mode.Boss)
                {
                    int left = 0;
                    foreach (var o in Target.All) if (o.Alive) left++;
                    if (left == 0) EndDungeon(true);
                }
            }
            else if (t is Ore o && mode == Mode.Mine)
            {
                if (o.reward > 0)
                {
                    dungeonGems += o.reward;
                    Fx.I?.Text(o.Center + Vector2.up * 0.4f, "+" + o.reward.ToString("0.#"), new Color(0.5f, 0.85f, 1f), 40, 0.9f);
                }
                // 11-05 보상 있는 광석(다이아)을 모두 깨면 잠깐 뒤 자동으로 끝 (회색 바위 = 보상 0 은 안 셈)
                if (MineRewardLeft == 0 && mineDoneT <= 0) mineDoneT = MineDoneDelay;
            }
        }

        // =====================================================================
        // 강화
        // =====================================================================
        public bool TryUpgrade(int which, bool auto = false)
        {
            int lv = which == 0 ? S.lvAtk : which == 1 ? S.lvSpd : S.lvGold;
            double cost = Stats.Cost(lv);
            if (S.gold < cost) return false;
            S.gold -= cost;
            if (which == 0) S.lvAtk++; else if (which == 1) S.lvSpd++; else S.lvGold++;
            if (!S.tutorialDone) S.tutorialDone = true;
            upgradeHint = false;
            if (!auto) AudioManager.Play(AudioManager.Sfx.Upgrade);   // 자동강화는 조용히 (0.5초마다 울리면 시끄러움)
            Notify();
            return true;
        }

        // 자동강화 패키지 (10-30, 상품 auto_upgrade): 켜 두면 0.5초마다 공격·속도·돈 중 가장 싼 강화를 골드가 되는 만큼 하나씩
        float autoT;
        public bool AutoUpgradeOn => S.autoUpgrade && S.autoOn;

        void AutoUpgradeTick()
        {
            if (!AutoUpgradeOn) return;
            autoT += Time.unscaledDeltaTime;
            if (autoT < 0.5f) return;
            autoT = 0;
            int best = 0;
            double bestCost = double.MaxValue;
            int[] lv = { S.lvAtk, S.lvSpd, S.lvGold };
            for (int i = 0; i < 3; i++) { double c = Stats.Cost(lv[i]); if (c < bestCost) { bestCost = c; best = i; } }
            TryUpgrade(best, true);
        }

        /// <summary>HUD [자동강화]: 가졌으면 켜기/끄기, 아니면 구매</summary>
        public void AutoUpgradeButton()
        {
            if (!S.autoUpgrade) { Iap.Buy("auto_upgrade"); return; }
            S.autoOn = !S.autoOn;
            HUD.I?.Toast(S.autoOn ? L.T("자동강화 켬") : L.T("자동강화 끔"));
            Notify();
        }

        // =====================================================================
        // 기능 열림 (기획서 3장)
        // =====================================================================
        // 기능 열림 = Unlock_Roadmap 표 (Stage = 그 스테이지까지 가 봤으면, Gems_Or_Challenge = 다이아를 모았거나 도전을 깼으면)
        public bool Unlocked(string id)
        {
            var u = Tables.Unlock(id);
            if (u == null) return true;
            if (u.type == "Gems_Or_Challenge") return S.weaponUnlocked || S.challengeCleared.Count > 0 || S.gems >= u.value;
            return S.bestEver >= u.stageIdx;
        }
        /// <summary>잠겨 있을 때 토스트 글</summary>
        public string LockMsg(string id) { var u = Tables.Unlock(id); return u != null && u.lockMsg.Length > 0 ? L.T(u.lockMsg) : L.T("아직 열리지 않았습니다"); }

        public bool WeaponOpen => Unlocked("weapon");
        public bool SkinOpen => Unlocked("skin");
        public bool MenuOpen => Unlocked("mission");
        public bool MineOpen => Unlocked("mine");
        public bool FishOpen => Unlocked("supply");
        public bool BossRaidOpen => Unlocked("boss_raid");

        // =====================================================================
        // 총알: 뽑기 · 합성 · 장착 (기획서 6장)
        // =====================================================================
        public string Gacha()
        {
            double cost = Tables.C("GachaCost");
            if (S.gems < cost) return L.T("다이아가 부족합니다");
            int empty = S.inv.IndexOf(-1);
            if (empty < 0) return L.T("인벤토리가 가득 찼습니다");
            S.gems -= cost;
            S.inv[empty] = Tables.Code(0, Tables.RandomType(0));   // Merge_Weight 비중
            S.weaponUnlocked = true;
            AudioManager.Play(AudioManager.Sfx.Merge);
            S.SaveSoon();   // 11-12 다이아가 움직인 동작은 곧 저장소에 (아래 분해·구매·미션도)
            Notify();
            return null;
        }

        /// <summary>11-14 사용자 "같은 무기가 아니어도 합성": Global_Config Merge_Same_Grade 1 = 같은 등급이면 종류가 달라도 합성 · 0 = 같은 탄알끼리만(11-04)</summary>
        public static bool MergeAnyType => Tables.C("Merge_Same_Grade", 1) >= 0.5;
        /// <summary>두 총알을 합성할 수 있는 짝인지 (등급 · 종류 규칙만 — 최고 등급은 따로)</summary>
        public static bool CanMergePair(int ca, int cb) => ca >= 0 && cb >= 0 && (MergeAnyType ? Tables.GradeOf(ca) == Tables.GradeOf(cb) : ca == cb);

        /// <summary>인벤토리 a 칸을 b 칸에 겹치기: 합성할 수 있는 짝(CanMergePair)이면 한 단계 위 등급의 랜덤 종류로 합성.
        /// 11-04 요청_02: 등급이 같아도 종류가 다르면 합성 안 됨 → 11-14 같은 등급이면 종류가 달라도 합성 (Merge_Same_Grade)</summary>
        public string Merge(int a, int b)
        {
            if (a == b) return "";
            int ca = S.inv[a], cb = S.inv[b];
            if (ca < 0) return "";
            if (cb < 0) { S.inv[b] = ca; S.inv[a] = -1; Notify(); return null; }   // 빈칸으로 옮기기
            int g = Tables.GradeOf(ca);
            if (g != Tables.GradeOf(cb)) { S.inv[a] = cb; S.inv[b] = ca; Notify(); return null; }  // 등급 다르면 자리 바꾸기
            if (!CanMergePair(ca, cb)) return L.T("같은 탄알끼리만 합성할 수 있습니다");
            if (g >= Tables.MaxGrade) return L.T("최고 등급입니다");
            S.inv[a] = -1;
            S.inv[b] = Tables.Code(g + 1, Tables.RandomType(g + 1));   // 한 단계 위 등급, Merge_Weight 비중 (10-24: 일반탄 각 18% · 레이저/미사일 각 14%)
            S.statMerges++;
            AudioManager.Play(AudioManager.Sfx.Merge);
            Notify();
            return null;
        }

        /// <summary>11-05 [일괄 합성] (추가 요청/UI 수정.png "조건이 맞는 1단계씩만"): 인벤토리의 합성할 수 있는 짝(11-14: 같은 등급) 두 개씩 한 번만 합성.
        /// 이번에 새로 나온 총알은 다시 합치지 않음(한 번 누르면 한 단계) · 장착 중인 총알 · 최고 등급은 빼고. 결과는 앞 칸에. 돌려줌 = 결과가 생긴 칸들</summary>
        public List<int> MergeAll()
        {
            var made = new List<int>();
            var snap = new List<int>(S.inv);
            var used = new bool[snap.Count];
            for (int i = 0; i < snap.Count; i++)
            {
                int c = snap[i];
                if (c < 0 || used[i] || Tables.GradeOf(c) >= Tables.MaxGrade) continue;
                for (int j = i + 1; j < snap.Count; j++)
                {
                    if (used[j] || !CanMergePair(c, snap[j])) continue;
                    used[i] = used[j] = true;
                    int g = Tables.GradeOf(c);
                    S.inv[j] = -1;
                    S.inv[i] = Tables.Code(g + 1, Tables.RandomType(g + 1));   // Merge 와 같은 규칙
                    S.statMerges++;
                    made.Add(i);
                    break;
                }
            }
            if (made.Count > 0) { AudioManager.Play(AudioManager.Sfx.Merge); Notify(); }
            return made;
        }

        /// <summary>11-05 [분해] 여러 개 고르기: 고른 인벤토리 칸들을 한 번에 분해 → 받은 다이아 합</summary>
        public double SalvageMany(IEnumerable<int> invIdx)
        {
            double sum = 0; int n = 0;
            foreach (int i in invIdx)
            {
                if (i < 0 || i >= S.inv.Count || S.inv[i] < 0) continue;
                sum += Tables.Grade(Tables.GradeOf(S.inv[i])).salvage;
                S.inv[i] = -1; n++;
            }
            if (n == 0) return 0;
            S.gems += sum;
            S.SaveSoon();
            AudioManager.Play(AudioManager.Sfx.Coin);
            HUD.I?.Bump(true);
            Notify();
            return sum;
        }

        /// <summary>인벤토리 총알 분해 → 다이아 (Grade_Enchant_Multiplier 의 Salvage_Gems_Reward)</summary>
        public string Salvage(int invIdx)
        {
            int c = S.inv[invIdx];
            if (c < 0) return "";
            double g = Tables.Grade(Tables.GradeOf(c)).salvage;
            S.inv[invIdx] = -1;
            S.gems += g;
            S.SaveSoon();
            AudioManager.Play(AudioManager.Sfx.Coin);
            HUD.I?.Bump(true);
            Notify();
            return null;
        }

        // =====================================================================
        // 총알 슬롯 강화 (Weapon_Enchant) — 장착 칸 3개마다 레벨, 다이아 + 골드, 성공 확률
        // =====================================================================
        /// <summary>ok = 성공 여부. 돌려주는 글 = 못 한 이유(null = 시도함)</summary>
        public string EnchantSlot(int slot, out bool ok)
        {
            ok = false;
            if (!Stats.SlotEnchantOn) return L.T("슬롯 강화는 없어졌습니다");   // 11-05 밤 제거
            int code = S.slots[slot];
            if (code < 0) return L.T("총알을 장착한 칸만 강화할 수 있습니다");
            int lv = S.slotLv[slot], max = Stats.SlotMax(slot);
            if (lv >= Tables.EnchantTop) return L.T("최고 강화입니다");
            if (lv >= max) return L.F("{0} 등급 총알은 +{1} 까지 — 더 높은 등급을 장착하세요", Tables.GradeName(Tables.GradeOf(code)), max);
            var e = Tables.Enchant(lv + 1);
            if (S.gems < e.gems) return L.T("다이아가 부족합니다");
            if (S.gold < e.gold) return L.T("골드가 부족합니다");
            S.gems -= e.gems;
            S.gold -= e.gold;
            ok = Random.value * 100 < e.rate;
            if (ok) S.slotLv[slot] = lv + 1;
            S.SaveSoon();
            AudioManager.Play(ok ? AudioManager.Sfx.Upgrade : AudioManager.Sfx.Fail);
            Notify();
            return null;
        }

        // =====================================================================
        // 보급품 (Supply_Collection) — 다이아로 사면 보유 효과(공격·속도·돈·보스 피해) 합산
        // =====================================================================
        public int BestChapter => S.bestEver / 5 + 1;
        public string BuySupply(int id)
        {
            var r = Tables.Supplies.Find(x => x.id == id);
            if (r == null) return "";
            if (S.supplies.Contains(id)) return L.T("이미 가진 보급품입니다");
            if (BestChapter < r.unlockChapter) return L.F("챕터 {0} 에서 열립니다", r.unlockChapter);
            if (S.gems < r.cost) return L.T("다이아가 부족합니다");
            S.gems -= r.cost;
            S.supplies.Add(id);
            S.SaveSoon();
            AudioManager.Play(AudioManager.Sfx.Upgrade);
            Notify();
            return null;
        }

        // =====================================================================
        // 상점: 다이아로 사는 골드 상자 (Shop_Products 의 Gems 가격, 'StageGold * 600' = 지금 스테이지 좀비 600마리 골드)
        // =====================================================================
        public string BuyShop(ShopRow r)
        {
            double price = Digits(r.priceValue);
            if (S.gems < price) return L.T("다이아가 부족합니다");
            int k = r.reward.IndexOf('*');
            double n = k >= 0 ? Digits(r.reward.Substring(k + 1)) : 600;
            double g = Stats.ZombieGold(S.stage) * n * Stats.GoldMul;
            S.gems -= price;
            S.gold += g;
            S.SaveSoon();
            AudioManager.Play(AudioManager.Sfx.Coin);
            HUD.I?.Bump(false);
            HUD.I?.Toast(L.T("골드") + " +" + Num.F(g));
            Notify();
            return null;
        }

        /// <summary>"100 다이아" → 100, "$1.99" → 1.99</summary>
        public static double Digits(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s ?? "")
            {
                if (char.IsDigit(ch) || ch == '.') sb.Append(ch);
                else if (ch == ',') continue;
                else if (sb.Length > 0) break;
            }
            return double.TryParse(sb.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        /// <summary>인벤토리 칸 → 장착 칸 (장착 칸에 있던 것은 인벤토리로)</summary>
        public string Equip(int invIdx, int slot)
        {
            int c = S.inv[invIdx];
            if (c < 0) return "";
            S.inv[invIdx] = S.slots[slot];
            S.slots[slot] = c;
            CompactSlots();
            Squad.I.Rebuild();
            Notify();
            return null;
        }

        /// <summary>장착 해제 (최소 1개는 장착해야 함)</summary>
        public string Unequip(int slot, int toInv = -1)
        {
            if (S.slots[slot] < 0) return "";
            int n = 0; foreach (var s in S.slots) if (s >= 0) n++;
            if (n <= 1) return L.T("최소 1개의 총알을 장착해야 합니다");
            if (toInv < 0) toInv = S.inv.IndexOf(-1);
            if (toInv < 0) return L.T("인벤토리가 가득 찼습니다");
            if (S.inv[toInv] >= 0) return L.T("빈 칸에만 놓을 수 있습니다");
            S.inv[toInv] = S.slots[slot];
            S.slots[slot] = -1;
            CompactSlots();
            Squad.I.Rebuild();
            Notify();
            return null;
        }

        public void SwapSlots(int a, int b)
        {
            (S.slots[a], S.slots[b]) = (S.slots[b], S.slots[a]);
            CompactSlots();
            Squad.I.Rebuild();
            Notify();
        }

        /// <summary>리더 칸(0)이 비지 않게 앞으로 당긴다</summary>
        void CompactSlots()
        {
            var l = new List<int>();
            foreach (var s in S.slots) if (s >= 0) l.Add(s);
            for (int i = 0; i < S.slots.Count; i++) S.slots[i] = i < l.Count ? l[i] : -1;
        }

        // =====================================================================
        // 쿠폰 (11-12): Coupon_List 표(Table/Coupon.xlsx) — 서버 없이 앱 안 표로 확인 · 같은 쿠폰은 세이브(클라우드)마다 한 번
        //   Code(대소문자 · 띄어쓰기 무시) · Reward_Type(Gems · Gold) · Reward_Value · Start_Date/End_Date(yyyy-MM-dd, 폰 날짜 · 비우면 제한 없음)
        //   새 쿠폰은 표에 줄만 더하면 되지만 앱 안 표라 업데이트에 실어야 폰에 들어감
        // =====================================================================
        public static string CouponKey(string code) => (code ?? "").Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();

        /// <summary>쿠폰 쓰기 — 성공이면 null + reward 에 받은 글자, 실패면 안내 글자</summary>
        public string UseCoupon(string code, out string reward)
        {
            reward = null;
            string key = CouponKey(code);
            if (key.Length == 0) return L.T("쿠폰 번호를 넣어 주세요");
            Tables.Row row = null;
            foreach (var r in Tables.ReadTable("Coupon_List")) if (CouponKey(r.S("Code")) == key) { row = r; break; }
            if (row == null) return L.T("없는 쿠폰 번호입니다");
            if (S.coupons.Contains(key)) return L.T("이미 사용한 쿠폰입니다");
            string today = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            string from = row.S("Start_Date"), to = row.S("End_Date");
            if (from.Length > 0 && string.CompareOrdinal(today, from) < 0) return L.T("아직 사용할 수 없는 쿠폰입니다");
            if (to.Length > 0 && string.CompareOrdinal(today, to) > 0) return L.T("기간이 지난 쿠폰입니다");
            double v = row.D("Reward_Value");
            string type = row.S("Reward_Type");
            if (v <= 0) return L.T("없는 쿠폰 번호입니다");
            if (type == "Gems") { S.gems += v; reward = L.T("다이아") + " " + v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }
            else if (type == "Gold") { S.gold += v; reward = L.T("골드") + " " + Num.F(v); }
            else return L.T("없는 쿠폰 번호입니다");
            S.coupons.Add(key);
            S.SaveSoon();
            AudioManager.Play(AudioManager.Sfx.Upgrade);
            Notify();
            Debug.Log($"[Coupon] {key} → {type} {v}");
            return null;
        }

        // =====================================================================
        // 스킨 (기획서 7.1 — 가진 스킨 능력치는 전부 합산)
        // =====================================================================
        public string BuySkin(int id)
        {
            var r = Tables.Skins.Find(x => x.id == id);
            if (r == null) return "";
            if (S.skins.Contains(id)) { S.skin = id; Squad.I.SetSkin(id); Notify(); return null; }
            if (S.gems < r.price) return L.T("다이아가 부족합니다");
            S.gems -= r.price;
            S.skins.Add(id);
            S.skin = id;
            S.SaveSoon();
            Squad.I.SetSkin(id);
            AudioManager.Play(AudioManager.Sfx.Upgrade);
            Notify();
            return null;
        }

        // =====================================================================
        // 버프 (광고 5분, 연속 시청 시 시간 누적, 재시청 대기 12초)
        // =====================================================================
        public string Buff(bool atk)
        {
            S.ClampTimers();
            double now = SaveData.NowD;
            double cool = atk ? S.buffAtkCool : S.buffGoldCool;
            if (cool > now) return L.F("{0}초 뒤에 다시 볼 수 있습니다", Math.Ceiling(cool - now));
            var b = Tables.Buff(atk ? "buff_atk" : "buff_gold");
            AdService.Show(b.id, () =>
            {
                // 연속 시청 = 시간 누적 (최대 Max_Stack_Sec 까지)
                double t = SaveData.NowD;
                double cap = b.maxStack > 0 ? t + b.maxStack : double.MaxValue;
                if (atk) { S.buffAtkEnd = Math.Min(Math.Max(S.buffAtkEnd, t) + b.duration, cap); S.buffAtkCool = t + b.cooldown; }
                else { S.buffGoldEnd = Math.Min(Math.Max(S.buffGoldEnd, t) + b.duration, cap); S.buffGoldCool = t + b.cooldown; }
                Notify();
            });
            return null;
        }

        // =====================================================================
        // 미션 (기획서 10.1 — 단계별 반복)
        // =====================================================================
        /// <summary>미션 진행 값 (Mission_Master 의 Save_Stat_Key)</summary>
        public long MissionValue(int i) => Tables.Missions[i].statKey switch
        {
            "statKills" => S.statKills, "statStages" => S.statStages, "statMines" => S.statMines,
            "statBossKills" => S.statBossKills, "statMerges" => S.statMerges, _ => 0
        };

        // 처치 미션 목표는 몰려오기 배수만큼 (좀비가 Horde_Mul 배로 많아져도 단계 속도는 그대로)
        public long MissionTarget(int i)
        {
            long t = Tables.MissionTarget(i, S.missionTier[i]);
            return Tables.Missions[i].statKey == "statKills" ? (long)Math.Round(t * (double)HordeMul) : t;
        }
        public bool MissionDone(int i) => Tables.MissionDone(i, S.missionTier[i]);

        // 보상 = 다이아로 친 값(Mission_Master). 10-24 사용자 요청: 한 번에 한 종류만 — 단계마다 골드 → 다이아 → 골드 … 번갈아.
        // 두 단계를 합친 양은 예전(다이아 Mission_Gem_Ratio 30% + 골드 70%)과 같게: 다이아 차례 = 값 x 0.3 x 2, 골드 차례 = 값 x 0.7 x 2.
        // 골드 1다이아어치 = 지금 스테이지 좀비 Gem_Gold_Kills(6)마리 골드 x 돈 배율 (상점 골드 상자 100다이아 = 600마리와 같은 비율)
        static double GemRatio => Tables.C("Mission_Gem_Ratio", 0.3);
        /// <summary>이번 단계 보상이 다이아인지 (짝수 단계 0·2·4… = 골드, 홀수 = 다이아)</summary>
        public bool MissionIsGem(int i) => S.missionTier[i] % 2 == 1;
        public double MissionGems(int i) => MissionIsGem(i) ? Math.Max(1, Math.Round(Tables.MissionReward(i, S.missionTier[i]) * GemRatio * 2)) : 0;
        public double MissionGold(int i) => MissionIsGem(i) ? 0 : Tables.MissionReward(i, S.missionTier[i]) * (1 - GemRatio) * 2 * Tables.C("Gem_Gold_Kills", 6) * Stats.ZombieGold(S.stage) * Stats.GoldMul;
        /// <summary>[받기] 버튼·토스트 글자: "다이아 N" 또는 "골드 M"</summary>
        public string MissionRewardText(int i) => MissionIsGem(i) ? $"{L.T("다이아")} {Num.Gem(MissionGems(i))}" : $"{L.T("골드")} {Num.F(MissionGold(i))}";

        public bool MissionReady(int i) => !MissionDone(i) && MissionValue(i) >= MissionTarget(i);

        public string ClaimMission(int i)
        {
            if (MissionDone(i)) return L.T("모든 단계를 끝냈습니다");
            if (!MissionReady(i)) return L.T("아직 달성하지 못했습니다");
            bool gem = MissionIsGem(i);
            double gems = MissionGems(i), gold = MissionGold(i);
            S.gems += gems;
            S.gold += gold;
            S.missionTier[i]++;
            S.SaveSoon();
            AudioManager.Play(AudioManager.Sfx.Coin);
            HUD.I?.Bump(gem);
            HUD.I?.Toast(gem ? L.T("다이아") + " +" + Num.Gem(gems) : L.T("골드") + " +" + Num.F(gold));
            Notify();
            return null;
        }

        // =====================================================================
        // 광고 무료 다이아 (상점 · 하루 AdGems_Daily 번, 한 번에 AdGems_Amount)
        // =====================================================================
        public int AdGemsLeft { get { S.RefreshDaily(); return Math.Max(0, (int)Tables.C("AdGems_Daily", 3) - S.adGemUsed); } }

        public string AdGems()
        {
            if (AdGemsLeft <= 0) return L.T("오늘은 다 받았습니다 (내일 다시)");
            AdService.Show("free_gems", () =>
            {
                double g = Tables.C("AdGems_Amount", 40);
                S.adGemUsed++;
                S.gems += g;
                AudioManager.Play(AudioManager.Sfx.Coin);
                HUD.I?.Bump(true);
                HUD.I?.Toast(L.T("다이아") + " +" + Num.Gem(g));
                S.Save();
                Notify();
            });
            return null;
        }

        public bool AnyMissionReady { get { for (int i = 0; i < Tables.Missions.Count; i++) if (MissionReady(i)) return true; return false; } }

        // =====================================================================
        // 던전 공통
        // =====================================================================
        /// <summary>도전할 수 있는 가장 낮은 챕터 (지금 챕터까지, 이미 깬 챕터 제외). 없으면 0</summary>
        public int ChallengeChapter
        {
            get
            {
                for (int c = 1; c <= S.Chapter; c++) if (!S.challengeCleared.Contains(c)) return c;
                return 0;
            }
        }

        public string StartChallenge()
        {
            int ch = ChallengeChapter;
            if (ch == 0) return L.T("다음 챕터에서 새 도전이 열립니다");
            var row = Tables.Challenge(ch);
            BeginDungeon(Mode.Challenge, row.time);
            dungeonChapter = ch;
            int n = row.count;
            double hp = row.hp;
            for (int i = 0; i < n; i++)
            {
                var r = PickZombie(ch, 100000 + ch);   // 도전도 3~5종 (스테이지와 다른 번호)
                // 멀리(오른쪽)에 많이: AI 가 가까운 것만 쫓으면 동선이 낭비되도록
                var p = Field.I.RandomRoad(i < n / 3 ? 0.55f : 0.15f, 1f, 0.6f);
                var z = Zombie.Spawn(r, p, hp * r.hpMul, 0, tint: PickTint(ch, false));
                z.standStill = true;
            }
            dungeonTotal = n;
            HUD.I?.Banner(L.F("도전 {0} — 모두 처치!", ch), new Color(1f, 0.85f, 0.4f));
            return null;
        }

        public string StartMine()
        {
            if (!MineOpen) return LockMsg("mine");
            S.RefreshDaily();
            if (S.mineTickets <= 0) return L.T("오늘 티켓을 다 썼습니다");
            S.mineTickets--;
            S.statMines++;
            BeginDungeon(Mode.Mine, Tables.Cf("MineTime"));
            dungeonGems = 0;
            mineDoneT = 0;
            // Mine_Ore_Spec 줄마다 Spawn_Count 개 (Spawn_X_Near~Far: 0 = 분대 바로 앞 ~ 1 = 오른쪽 끝), 보상 x Mine_Scaling 배율
            // 큰 보석 = 오른쪽 끝(멀리) 밀집, 작은 보석 = 곳곳, 회색 바위 = 분대 가까이(AI 가 먼저 때리는 함정)
            double mul = Tables.MineMul(S.Chapter);
            foreach (var o in Tables.Ores)
                for (int i = 0; i < o.count; i++) Ore.Spawn(o, Field.I.RandomRoad(o.near, o.far, o.scale >= 1 ? 0.7f : 0.6f), mul);
            HUD.I?.Banner(L.T("비밀던전"), new Color(0.5f, 0.85f, 1f));
            return null;
        }

        public string StartBossRaid()
        {
            if (!BossRaidOpen) return LockMsg("boss_raid");
            S.RefreshDaily();
            if (S.bossTickets <= 0) return L.T("오늘 티켓을 다 썼습니다");
            S.bossTickets--;
            var row = Tables.BossRaid(S.bossLv);
            BeginDungeon(Mode.Boss, row.time);
            var r = Tables.Zombie(string.IsNullOrEmpty(row.bossId) ? "ogre" : row.bossId);
            var z = Zombie.Spawn(r, Field.I.RandomRoad(0.5f, 0.62f, 1.4f), row.hp, 0, row.scale, true, PickTint(Mathf.Max(1, row.recChapter), true));
            z.standStill = true;
            dungeonTotal = 1;
            AudioManager.Play(AudioManager.Sfx.BossAppear);
            HUD.I?.Banner(L.F("보스 던전 Lv.{0}", S.bossLv + 1), new Color(1f, 0.4f, 0.3f));
            return null;
        }

        void BeginDungeon(Mode m, float time)
        {
            Fx.I?.FlushMoney();
            ClearTargets();
            mode = m;
            dungeonTime = time;
            dungeonOver = false;
            bossPhase = false; boss = null;
            Squad.I.ResetPosition();
            ApplyTint();
            StageChanged?.Invoke();
        }

        /// <summary>비밀던전에 남은 보상 있는 광석 수</summary>
        public int MineRewardLeft { get { int n = 0; foreach (var t in Target.All) if (t is Ore o && o.Alive && o.reward > 0) n++; return n; } }
        const float MineDoneDelay = 0.8f;   // 마지막 +다이아 글자를 보고 끝나게
        float mineDoneT;

        void DungeonTick(float dt)
        {
            if (dungeonOver) return;
            if (mode == Mode.Mine && mineDoneT > 0)
            {
                mineDoneT -= dt;
                if (mineDoneT <= 0) { EndDungeon(true); return; }
            }
            dungeonTime -= dt;
            if (dungeonTime <= 0)
            {
                dungeonTime = 0;
                EndDungeon(mode == Mode.Mine);   // 광산은 시간 끝 = 정상 종료
            }
        }

        public int DungeonLeft { get { int n = 0; foreach (var t in Target.All) if (t.Alive) n++; return n; } }

        void EndDungeon(bool win)
        {
            if (dungeonOver) return;
            dungeonOver = true;
            double reward = 0;
            string title;
            if (mode == Mode.Challenge) { title = win ? L.F("도전 {0} 성공!", dungeonChapter) : L.T("시간 초과"); if (win) reward = Tables.Challenge(dungeonChapter).reward; }
            else if (mode == Mode.Mine) { title = L.T("채굴 끝!"); reward = Math.Round(dungeonGems, 1); }
            else { title = win ? L.T("보스 처치!") : L.T("시간 초과"); if (win) reward = Tables.BossRaid(S.bossLv).reward; }
            AudioManager.Play(win ? AudioManager.Sfx.Victory : AudioManager.Sfx.Fail);
            HUD.I?.ShowResult(title, win, reward);
        }

        /// <summary>결과 창에서 받기 (광고 = 배율)</summary>
        public void ClaimDungeon(double reward, bool win)
        {
            if (win || mode == Mode.Mine)
            {
                S.gems += reward;
                if (mode == Mode.Challenge && win && !S.challengeCleared.Contains(dungeonChapter)) S.challengeCleared.Add(dungeonChapter);
                if (mode == Mode.Boss && win) { S.bossLv++; S.statBossKills++; }
            }
            ExitDungeon();
        }

        public void ExitDungeon()
        {
            ClearTargets();
            mode = Mode.Stage;
            dungeonOver = false;
            Squad.I.ResetPosition();
            ApplyTint();
            S.Save();
            StageChanged?.Invoke();
            Notify();
        }

        public void ClearTargets()
        {
            spawnQueue.Clear();
            foreach (var t in Target.All.ToArray()) if (t) Destroy(t.gameObject);
            Target.All.Clear();
            Bullet.ClearAll();
        }

        // =====================================================================
        // 방치 보상 (11-04): 게임을 끄거나 백그라운드에 두는 동안에도 마지막 스테이지에서 사냥한 셈 치고 골드를 줌
        //   초당 골드 = 처치 속도 x 그 스테이지 좀비 골드 x 돈 배율(버프 뺌) x Offline_Rate
        //   처치 속도 = min(분대 초당 피해 ÷ 좀비 체력, 1 ÷ 등장 간격) — balance_sim.py 반복 사냥과 같은 식
        //   비운 시간은 Offline_MaxHours 까지 · Offline_MinMinutes 보다 짧으면 안 줌(광고·결제 창으로 잠깐 나간 것)
        // =====================================================================
        public double OfflineGoldPerSec
        {
            get
            {
                var st = Tables.Stage(S.stage);
                double hp = Stats.ZombieHp(S.stage);
                double byDps = hp > 0 ? Stats.SquadDps() / hp : 0;
                double bySpawn = 1.0 / Math.Max(0.05, st.spawn);
                return Math.Min(byDps, bySpawn) * Stats.ZombieGold(S.stage) * Stats.GoldMulBase * Tables.C("Offline_Rate", 0.5);
            }
        }

        public static double OfflineMaxSec => Tables.C("Offline_MaxHours", 8) * 3600;

        bool offlineReady;   // 첫 프레임 뒤(HUD 다 만든 뒤)부터

        /// <summary>마지막 저장 시각부터 지금까지 오래 비웠으면 방치 보상 창 (true = 창 띄움). 한 번 계산하면 소진(두 번 안 줌).
        /// 11-12: 지난번에 창만 띄우고 못 받은 시간(S.offlinePendingSec — 창이 뜬 채로 앱이 꺼짐)이 있으면 그것도 같이</summary>
        public bool CheckOffline()
        {
            if (!offlineReady || HUD.I == null) return false;
            long last = S.loadedLastSeen > 0 ? S.loadedLastSeen : S.lastSeen;
            S.loadedLastSeen = 0;
            S.lastSeen = Math.Max(S.lastSeen, SaveData.Now);   // 11-07 앞으로만 (시간을 뒤로 돌려도 줄지 않음)
            long away = last > 0 ? SaveData.Now - last : 0;
            if (away < Tables.C("Offline_MinMinutes", 5) * 60) away = 0;   // 잠깐 나갔다 옴 · 기기 시간을 뒤로 돌린 경우(음수)
            double pend = OfflinePopup.OpenUnclaimed ? 0 : S.offlinePendingSec;   // 창이 떠 있으면 창이 그 시간을 이미 들고 있음
            double sec = Math.Min(away + pend, OfflineMaxSec);
            if (sec <= 0 || OfflineGoldPerSec * sec <= 0) return false;
            OfflinePopup.Show(away, sec);
            return true;
        }

        System.Collections.IEnumerator OfflineAtStart()
        {
            yield return null;   // HUD 가 다 만들어진 뒤
            offlineReady = true;
            if (SaveData.LoadedFromBackup) { SaveData.LoadedFromBackup = false; HUD.I?.Toast(L.T("세이브를 읽지 못해 예비 세이브로 불러왔습니다")); }
            if (CloudSync.pendingToast != null) { HUD.I?.Toast(CloudSync.pendingToast); CloudSync.pendingToast = null; }
            CheckOffline();
            CheckAttend(true);   // 방치 보상 창 위에 출석 창 (출석부터 받고 → 방치 보상)
            CloudSync.AutoStart();   // 11-07 클라우드: 연결해 둔 기기면 자동 로그인 → 클라우드와 맞춤
            AppUpdateCheck.Start();  // 11-12 새 버전이 있으면 다른 창이 닫힌 뒤 [업데이트] 창 (실행에 한 번)
        }

        // =====================================================================
        // 출석 (11-04): 하루 한 번(기기 날짜) 접속 창 — Attend_Gems_1·2·3 다이아를 차례로, 다 받으면 1일차부터 다시 (11-14 7일: 100·200·300·500·700·1000·1500)
        //   빠진 날이 있어도 끊기지 않고 다음 칸 · 처음 켠 날부터 · 스테이지 화면에서만(던전 중엔 끝난 뒤)
        // =====================================================================
        public static int AttendDays => Math.Max(1, Tables.Ci("Attend_Days"));
        public static double AttendGems(int i) => Tables.C("Attend_Gems_" + (i + 1), 100 * (i + 1));

        /// <summary>오늘 출석을 아직 안 받았으면 출석 창 (true = 띄움). overPopups = 다른 창이 떠 있어도 (켤 때·돌아올 때)</summary>
        public bool CheckAttend(bool overPopups)
        {
            if (!offlineReady || HUD.I == null || S.AttendToday) return false;
            if (mode != Mode.Stage) return false;   // 11-04 밤: 처음 켰을 때부터 (전엔 첫 강화·메뉴 열린 뒤부터라 처음엔 안 떴음)
            var cur = HUD.I.Get<AttendPopup>();
            if (cur != null && cur.gameObject.activeSelf) return false;
            if (!overPopups && HUD.I.PopupOpen) return false;
            AttendPopup.Show();
            return true;
        }

        /// <summary>오늘 출석 받기: 다이아 지급 · 받은 칸 번호(0부터) 돌려줌, 이미 받았으면 -1</summary>
        public int ClaimAttend()
        {
            if (S.AttendToday) return -1;
            int i = S.attendCount % AttendDays;
            S.gems += AttendGems(i);
            S.attendCount++;
            S.attendDay = SaveData.Today;
            S.Save();
            Notify();
            return i;
        }

        void OnApplicationPause(bool p)
        {
            if (p) { Fx.I?.FlushMoney(); S.Save(); }
            else { CheckOffline(); CheckAttend(true); }   // 백그라운드에서 돌아옴 — 나갈 때 저장한 시각부터 (켤 때 불리는 것은 offlineReady 전이라 무시)
        }
        void OnApplicationQuit() { Fx.I?.FlushMoney(); S.Save(); }
    }
}
