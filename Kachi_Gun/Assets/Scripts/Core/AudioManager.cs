using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 사운드 매니저: 외부 음원 파일 없이 코드로 직접 합성(Procedural)하여 배경음(BGM 2종)과 효과음(SFX 16종)을 재생한다.
    /// 씬에 없어도 게임 시작 시 자동 생성되며(DontDestroyOnLoad), 설정 창의 배경음·효과음 켜기/끄기(SaveData.bgm / sfx)와 즉시 연동된다.
    /// 10-26 다듬기 (사운드_검토.html): 배경음 40초 안팎 여러 구간 + 드럼 · 폰 스피커용 높은 소리 보강 · 돈 소리 줄임 ·
    /// 전투 소리는 채널 10개까지(나머지는 중요한 소리 몫) · 광산 소리 줄임 · 배경음 0.5초 겹쳐 바꾸기 · 보스 처치/승리 구분
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I;

        public enum Sfx
        {
            Shot,        // 일반 총 발사 (기본·속사·관통)
            Shotgun,     // 산탄 발사
            Laser,       // 레이저 발사
            Missile,     // 미사일 발사
            Boom,        // 미사일 폭발
            Hit,         // 좀비 피격
            MineHit,     // 광산 크리스탈 타격
            Dead,        // 좀비 처치
            BossAppear,  // 보스 등장 경고
            BossDead,    // 보스 처치
            Upgrade,     // 능력치 강화 · 슬롯 강화 성공 · 스킨/보급품 구매
            Merge,       // 총알 합성 · 뽑기
            Coin,        // 골드·다이아 획득 (연속 획득 시 피치 상승, 맨 위에 닿으면 처음 음으로)
            UiClick,     // UI 버튼 클릭
            Victory,     // 던전 승리
            Fail         // 보스 실패 · 던전 실패 · 슬롯 강화 실패
        }

        [Range(0f, 1f)] public float bgmVolume = 0.22f;
        [Range(0f, 1f)] public float sfxVolume = 0.48f;
        public int channels = 16;
        /// <summary>전투 소리(총·맞음·죽음·돈·광산)가 쓸 수 있는 채널 수. 나머지는 보스·승리·합성·버튼 같은 중요한 소리 몫</summary>
        public int combatChannels = 10;

        const float BgmFadeSec = 0.5f;
        AudioSource[] bgmPlayers = new AudioSource[2];   // 배경음 바꿀 때 0.5초 겹치기용 2개
        AudioLowPassFilter[] bgmFilters = new AudioLowPassFilter[2];
        int bgmCur;
        float bgmFadeT = 1f;
        AudioClip stageBgm;
        AudioClip bossBgm;

        AudioSource[] sfxPlayers;
        int channelIndex;

        readonly Dictionary<Sfx, AudioClip> sfxClips = new();
        readonly Dictionary<Sfx, float> lastPlayTime = new();

        float coinPitch = 1.0f, lastCoinTime;
        float upgPitch = 1.0f, lastUpgTime;
        Selectable pressed;

        const int SAMPLE_RATE = 44100;
        const float FadeInSec = 0.003f;

        static SaveData S => SaveData.Cur;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
        }

        /// <summary>어디서든 효과음 재생</summary>
        public static void Play(Sfx sfx)
        {
            if (I != null) I.PlaySfx(sfx);
        }

        /// <summary>무기 종류별 발사음 재생</summary>
        public static void PlayShoot(string weaponType)
        {
            if (I == null) return;
            Sfx sfx = weaponType switch
            {
                "laser" => Sfx.Laser,
                "missile" => Sfx.Missile,
                "shotgun" => Sfx.Shotgun,
                _ => Sfx.Shot
            };
            I.PlaySfx(sfx);
        }

        /// <summary>설정 창에서 배경음·효과음 토글 시 호출</summary>
        public static void Apply()
        {
            if (I != null) I.ApplySettings();
        }

        /// <summary>스테이지·보스·던전 전환 시 배경음 갱신</summary>
        public static void RefreshBgm()
        {
            if (I != null) I.UpdateBgmClip();
        }

        /// <summary>절전모드 등에서 배경음을 먹먹하게(로우패스 필터)</summary>
        public static void Muffle(bool on)
        {
            if (I == null) return;
            foreach (var f in I.bgmFilters) if (f != null) f.enabled = on;
        }

        /// <summary>시험용: 일반 배경음 길이(초)</summary>
        public static float StageBgmSeconds => I != null && I.stageBgm != null ? I.stageBgm.length : 0f;

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Init()
        {
            if (FindFirstObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            for (int i = 0; i < 2; i++)
            {
                var bgmObj = new GameObject("BgmPlayer" + i);
                bgmObj.transform.SetParent(transform, false);
                var p = bgmObj.AddComponent<AudioSource>();
                p.playOnAwake = false;
                p.loop = true;
                p.volume = 0f;
                bgmPlayers[i] = p;
                var f = bgmObj.AddComponent<AudioLowPassFilter>();
                f.cutoffFrequency = 800f;
                f.enabled = false;
                bgmFilters[i] = f;
            }

            var sfxObj = new GameObject("SfxPlayer");
            sfxObj.transform.SetParent(transform, false);
            sfxPlayers = new AudioSource[channels];
            for (int i = 0; i < channels; i++)
            {
                sfxPlayers[i] = sfxObj.AddComponent<AudioSource>();
                sfxPlayers[i].playOnAwake = false;
                sfxPlayers[i].volume = sfxVolume;
            }

            GenerateAllProceduralSounds();
            PrepareStageBgm();   // 11-15 새 평소 배경음은 계산이 길어서 따로 일꾼이 먼저 (ApplySettings 가 곧바로 씀)
            ApplySettings();
            PrepareBossBgm();
        }

        public void ApplySettings()
        {
            bool bgmOn = S == null || S.bgm;
            bool sfxOn = S == null || S.sfx;

            if (!bgmOn) StopBgm();
            else UpdateBgmClip();

            if (!sfxOn && sfxPlayers != null)
            {
                foreach (var p in sfxPlayers) p.Stop();
            }
        }

        void StopBgm()
        {
            foreach (var p in bgmPlayers) if (p != null) { p.Stop(); p.clip = null; p.volume = 0f; }
            bgmFadeT = 1f;
        }

        public void UpdateBgmClip()
        {
            if (bgmPlayers[0] == null) return;
            if (S != null && !S.bgm)
            {
                StopBgm();
                return;
            }

            var gm = GameManager.I;
            bool intense = gm != null && (gm.bossPhase || gm.mode != GameManager.Mode.Stage);
            AudioClip target = intense ? (bossBgm ??= TakeBossBgm()) : (stageBgm ??= GenerateStageBgm());

            var cur = bgmPlayers[bgmCur];
            if (cur.isPlaying && cur.clip == target) return;

            if (!cur.isPlaying || cur.clip == null)
            {
                // 처음 켤 때: 바로 시작
                cur.clip = target;
                cur.volume = bgmVolume;
                cur.Play();
                bgmFadeT = 1f;
                return;
            }
            // 바꿀 때: 새 곡을 다른 플레이어에서 시작하고 0.5초 동안 겹쳐서 바꿈 (Update)
            bgmCur = 1 - bgmCur;
            var next = bgmPlayers[bgmCur];
            next.clip = target;
            next.volume = 0f;
            next.Play();
            bgmFadeT = 0f;
        }

        public void PlaySfx(Sfx sfx)
        {
            if (S != null && !S.sfx) return;
            if (GameManager.I != null && GameManager.I.eco && sfx != Sfx.UiClick) return; // 절전모드 중에는 전투 효과음 생략

            float now = Time.unscaledTime;
            float minInterval = sfx switch
            {
                Sfx.Shot or Sfx.Shotgun or Sfx.Laser or Sfx.Missile => 0.125f,   // 종류마다 초당 최대 8번
                Sfx.Hit => 0.1f,
                Sfx.MineHit => 0.08f,
                Sfx.Coin => 0.1f,
                Sfx.Dead => 0.06f,
                Sfx.Boom => 0.12f,
                _ => 0.02f
            };
            float gap = lastPlayTime.TryGetValue(sfx, out float last) ? now - last : 99f;
            if (gap < minInterval) return;
            lastPlayTime[sfx] = now;

            if (!sfxClips.TryGetValue(sfx, out AudioClip clip) || clip == null) return;

            bool combat = IsCombat(sfx);
            int idx = FreeChannel(combat ? 0 : combatChannels, combat ? combatChannels : sfxPlayers.Length);
            if (idx < 0 && !combat) idx = FreeChannel(0, sfxPlayers.Length);   // 중요한 소리는 빈 곳 아무 데나
            if (idx < 0) return;

            channelIndex = idx;
            var player = sfxPlayers[idx];
            player.clip = clip;
            float vol = sfxVolume;
            // 발사음: 빨리 쏠수록 조금 작게 (0.25초 간격 이상이면 그대로, 0.125초면 65%)
            if (sfx == Sfx.Shot || sfx == Sfx.Shotgun || sfx == Sfx.Laser || sfx == Sfx.Missile)
                vol *= Mathf.Lerp(0.65f, 1f, Mathf.InverseLerp(0.125f, 0.25f, gap));
            player.volume = vol;

            if (sfx == Sfx.Coin)
            {
                // 연속으로 들어오면 음이 올라가고, 맨 위에 닿으면 다시 처음 음으로
                if (now - lastCoinTime < 0.45f) { coinPitch += 0.04f; if (coinPitch > 1.55f) coinPitch = 1.0f; }
                else coinPitch = 1.0f;
                lastCoinTime = now;
                player.pitch = coinPitch;
            }
            else if (sfx == Sfx.Upgrade)
            {
                upgPitch = now - lastUpgTime < 0.35f ? Mathf.Min(upgPitch + 0.035f, 1.45f) : 1.0f;
                lastUpgTime = now;
                player.pitch = upgPitch;
            }
            else if (sfx == Sfx.Shot || sfx == Sfx.Shotgun || sfx == Sfx.Laser || sfx == Sfx.Hit || sfx == Sfx.MineHit)
            {
                player.pitch = Random.Range(0.92f, 1.08f);
            }
            else
            {
                player.pitch = 1.0f;
            }

            player.Play();
        }

        static bool IsCombat(Sfx s) => s is Sfx.Shot or Sfx.Shotgun or Sfx.Laser or Sfx.Missile or Sfx.Boom
                                         or Sfx.Hit or Sfx.MineHit or Sfx.Dead or Sfx.Coin;

        /// <summary>[from, to) 채널 중 안 쓰는 칸 (마지막으로 쓴 칸 다음부터 찾음), 없으면 -1</summary>
        int FreeChannel(int from, int to)
        {
            int n = to - from;
            if (n <= 0) return -1;
            int start = Mathf.Clamp(channelIndex, from, to - 1) - from;
            for (int i = 0; i < n; i++)
            {
                int idx = from + (start + i) % n;
                if (!sfxPlayers[idx].isPlaying) return idx;
            }
            return -1;
        }

        // ---------- 버튼 클릭 소리: 누른 버튼과 뗀 자리 버튼이 같을 때 · 배경음 겹쳐 바꾸기 ----------
        void Update()
        {
            if (Input.GetMouseButtonDown(0)) pressed = SelectableUnder(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0))
            {
                if (pressed != null && pressed == SelectableUnder(Input.mousePosition))
                    PlaySfx(Sfx.UiClick);
                pressed = null;
            }

            if (bgmFadeT < 1f)
            {
                bgmFadeT = Mathf.Min(1f, bgmFadeT + Time.unscaledDeltaTime / BgmFadeSec);
                var next = bgmPlayers[bgmCur];
                var prev = bgmPlayers[1 - bgmCur];
                next.volume = bgmVolume * bgmFadeT;
                prev.volume = bgmVolume * (1f - bgmFadeT);
                if (bgmFadeT >= 1f) { prev.Stop(); prev.clip = null; }
            }
        }

        readonly List<RaycastResult> hits = new();

        Selectable SelectableUnder(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return null;
            hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPos }, hits);
            if (hits.Count == 0) return null;
            var s = hits[0].gameObject.GetComponentInParent<Selectable>();
            if (s == null || !s.IsInteractable() || !(s is Button || s is Toggle)) return null;
            if (s.targetGraphic != null && s.targetGraphic.color.a < 0.05f && s.name != "Close") return null;
            return s;
        }

        // =========================================================================
        // 절차적 사운드 합성 (외부 오디오 파일 없이 클립 생성)
        // 숫자를 바꾸면 tools/sound_review.py 의 표도 같이 바꿀 것 (소리 검토 wav·측정)
        // =========================================================================
        void GenerateAllProceduralSounds()
        {
            sfxClips[Sfx.Shot] = Clip("Sfx_Shot", SweepWithNoise(520f, 120f, 0.075f, 0.45f, 0.55f));
            sfxClips[Sfx.Shotgun] = Clip("Sfx_Shotgun", SweepWithNoise(340f, 70f, 0.13f, 0.65f, 0.70f));
            sfxClips[Sfx.Laser] = Clip("Sfx_Laser", Sweep(1350f, 260f, 0.095f, WaveType.Saw, 0.45f));
            sfxClips[Sfx.Missile] = Clip("Sfx_Missile", SweepWithNoise(280f, 95f, 0.16f, 0.55f, 0.55f));
            sfxClips[Sfx.Boom] = Clip("Sfx_Boom", SweepWithNoise(160f, 38f, 0.28f, 0.70f, 0.85f));
            // 맞음: 한 옥타브 위로 (폰 스피커용)
            sfxClips[Sfx.Hit] = Clip("Sfx_Hit", SweepWithNoise(420f, 130f, 0.065f, 0.5f, 0.5f));
            // 광산: 작게 (0.45 → 0.28)
            sfxClips[Sfx.MineHit] = Clip("Sfx_MineHit", Arpeggio(new[] { 1174.66f, 1567.98f }, 0.032f, WaveType.Triangle, 0.28f));
            // 좀비 죽음: 한 옥타브 위 + 짧은 "퍽" 노이즈
            sfxClips[Sfx.Dead] = Clip("Sfx_Dead", Mix(Sweep(480f, 960f, 0.08f, WaveType.Triangle, 0.36f), NoiseBurst(0.05f, 40f, 0.3f, true)));
            // 보스 등장: 한 옥타브 위 톱니+사각이 주인공, 원래 낮은 음은 살짝 (폰 스피커용)
            var bossNotes = new[] { 220f, 207.65f, 196f, 185f, 220f };
            sfxClips[Sfx.BossAppear] = Clip("Sfx_BossAppear", Mix(Mix(Arpeggio(Oct(bossNotes, 2f), 0.11f, WaveType.Saw, 0.55f), Arpeggio(Oct(bossNotes, 2f), 0.11f, WaveType.Square, 0.28f)), Arpeggio(bossNotes, 0.11f, WaveType.Saw, 0.15f)));
            // 보스 처치: 낮은 톱니(합성·승리보다 한 옥타브 아래) + 위 사각 + 처음에 "쿵"
            var bdNotes = new[] { 196f, 261.63f, 329.63f, 392f, 523.25f };
            sfxClips[Sfx.BossDead] = Clip("Sfx_BossDead", Mix(Mix(Arpeggio(bdNotes, 0.11f, WaveType.Saw, 0.45f), Arpeggio(Oct(bdNotes, 2f), 0.11f, WaveType.Square, 0.32f)), Thump(0.25f, 140f, 42f, 0.7f)));
            sfxClips[Sfx.Upgrade] = Clip("Sfx_Upgrade", Arpeggio(new[] { 523.25f, 659.25f, 783.99f }, 0.045f, WaveType.Triangle, 0.48f));
            sfxClips[Sfx.Merge] = Clip("Sfx_Merge", Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.075f, WaveType.Square, 0.5f));
            // 돈: 크기 절반 (0.45 → 0.225)
            sfxClips[Sfx.Coin] = Clip("Sfx_Coin", Arpeggio(new[] { 987.77f, 1318.51f }, 0.042f, WaveType.Sine, 0.225f));
            sfxClips[Sfx.UiClick] = Clip("Sfx_UiClick", Arpeggio(new[] { 659.25f, 880f }, 0.035f, WaveType.Triangle, 0.38f));
            // 승리: 마지막 음을 길게 (3배)
            sfxClips[Sfx.Victory] = Clip("Sfx_Victory", Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f }, 0.10f, WaveType.Square, 0.52f, 3f));
            // 실패: 원래 톱니파 + 한 옥타브 위 사각 + 두 옥타브 위 삼각 (폰 스피커용)
            var failNotes = new[] { 392.00f, 349.23f, 311.13f, 261.63f };
            sfxClips[Sfx.Fail] = Clip("Sfx_Fail", Mix(Mix(Arpeggio(failNotes, 0.15f, WaveType.Saw, 0.25f), Arpeggio(Oct(failNotes, 2f), 0.15f, WaveType.Square, 0.25f)), Arpeggio(Oct(failNotes, 4f), 0.15f, WaveType.Triangle, 0.15f)));
        }

        enum WaveType { Sine, Square, Saw, Triangle }

        static float[] Oct(float[] notes, float k)
        {
            var r = new float[notes.Length];
            for (int i = 0; i < notes.Length; i++) r[i] = notes[i] * k;
            return r;
        }

        /// <summary>두 소리 더하기 (길이는 긴 쪽)</summary>
        static float[] Mix(float[] a, float[] b)
        {
            var r = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < a.Length; i++) r[i] += a[i];
            for (int i = 0; i < b.Length; i++) r[i] += b[i];
            for (int i = 0; i < r.Length; i++) r[i] = Mathf.Clamp(r[i], -1f, 1f);
            return r;
        }

        static AudioClip Clip(string name, float[] data)
        {
            int fade = Mathf.Min(data.Length, Mathf.CeilToInt(SAMPLE_RATE * FadeInSec));
            for (int i = 0; i < fade; i++) data[i] *= (float)i / fade;
            int fadeOut = Mathf.Min(data.Length, Mathf.CeilToInt(SAMPLE_RATE * 0.003f));
            for (int i = 0; i < fadeOut; i++) data[data.Length - 1 - i] *= (float)i / fadeOut;
            var clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] Sweep(float startFreq, float endFreq, float duration, WaveType type, float gain)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                phase += Mathf.Lerp(startFreq, endFreq, t) / SAMPLE_RATE;
                data[i] = SampleWave(phase, type) * Mathf.Exp(-4f * t) * gain;
            }
            return data;
        }

        static float[] SweepWithNoise(float startFreq, float endFreq, float duration, float noiseRatio, float gain)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            float phase = 0f;
            var rng = new System.Random(12345);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                phase += Mathf.Lerp(startFreq, endFreq, t) / SAMPLE_RATE;
                float tone = SampleWave(phase, WaveType.Saw);
                float noise = (float)rng.NextDouble() * 2f - 1f;
                data[i] = Mathf.Lerp(tone, noise, noiseRatio) * Mathf.Exp(-5f * t) * gain;
            }
            return data;
        }

        /// <summary>notes 를 차례로. lastHold = 마지막 음 길이 배수</summary>
        static float[] Arpeggio(float[] notes, float stepDuration, WaveType type, float gain, float lastHold = 1f)
        {
            int stepSamples = Mathf.CeilToInt(SAMPLE_RATE * stepDuration);
            int lastSamples = Mathf.CeilToInt(stepSamples * lastHold);
            float[] data = new float[stepSamples * (notes.Length - 1) + lastSamples];
            float phase = 0f;
            int o = 0;
            for (int n = 0; n < notes.Length; n++)
            {
                int len = n == notes.Length - 1 ? lastSamples : stepSamples;
                float decay = n == notes.Length - 1 ? 2.5f / lastHold * 1.6f : 2.5f;
                for (int i = 0; i < len; i++)
                {
                    float t = (float)i / len;
                    phase += notes[n] / SAMPLE_RATE;
                    data[o + i] = SampleWave(phase, type) * Mathf.Exp(-decay * t) * gain;
                }
                o += len;
            }
            return data;
        }

        /// <summary>짧은 노이즈. highPass = 이웃 샘플 차이로 높은 소리만 (하이햇·"퍽")</summary>
        static float[] NoiseBurst(float duration, float decay, float gain, bool highPass, int seed = 777)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            var d = new float[n];
            var rng = new System.Random(seed);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float x = (float)rng.NextDouble() * 2f - 1f;
                float v = highPass ? (x - prev) * 0.5f : x;
                prev = x;
                d[i] = v * Mathf.Exp(-decay * t) * gain;
            }
            return d;
        }

        /// <summary>"쿵" (사인파 높낮이가 빠르게 내려감 = 킥 드럼)</summary>
        static float[] Thump(float duration, float f0, float f1, float gain)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(f0, f1, Mathf.Sqrt(t)) / SAMPLE_RATE;
                d[i] = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-6f * t) * gain;
            }
            return d;
        }

        // =========================================================================
        // 배경음: 32칸(8분음표) = 한 구간, 구간 여러 개를 이어 40초 안팎. 0 = 쉼표
        // 드럼 무늬는 한 마디 8칸 글자: k 킥 · s 스네어 · h 하이햇 · . 없음 (구간마다 되풀이)
        // =========================================================================
        struct Section
        {
            public float[] bass, lead;
            public string drums;
            public float leadDecay;
            public Section(float[] b, float[] l, string d, float ld = 4.2f) { bass = b; lead = l; drums = d; leadDecay = ld; }
        }

        static float[] R(float f, int n) { var a = new float[n]; for (int i = 0; i < n; i++) a[i] = f; return a; }
        static float[] Cat(params float[][] parts)
        {
            var l = new List<float>();
            foreach (var p in parts) l.AddRange(p);
            return l.ToArray();
        }

        const string DrumLight = ".h.h.h.h", DrumFull = "khshkhsh", DrumHalf = "k...h...", DrumDrive = "khshkkhs";

        // =====================================================================
        // 11-15 사용자 "배경음악을 조금 더 힐링되고 귀여운 음악으로" — 평소 배경음 SoftSong (tools/bgm_cute.py 와 같은 식)
        //   Config Bgm_Stage_Style: 0 = 예전 곡(120 BPM A단조) · 1 = A "봄 산책"(C장조 92 BPM) · 2 = B "낮잠"(F장조 84 BPM)
        //     · 3 = C "낮잠 경쾌"(11-15 사용자가 B 를 골라 "조금 더 경쾌하게 · 속도 1.5배": F장조 126 BPM · 통통 베이스 · 1·3 킥 2·4 틱 · 20마디)
        //   악기: 오르골(사인 + 2·3배음, 길게 울림) · 화음 패드 · 통통 베이스(1·3박 근음/5음) · 마림바 분산화음(엇박) · 아주 약한 킥/쉐이커/틱
        // =====================================================================
        public static int StageStyle => Mathf.Clamp(Tables.Ci("Bgm_Stage_Style"), 0, 3);
        System.Threading.Tasks.Task<float[]> stageTask;
        int stageTaskStyle = -1;
        void PrepareStageBgm()
        {
            int st = StageStyle;   // 표는 메인 스레드에서 읽고 일꾼에게 넘김
            if (st == 0 || stageTask != null) return;
            stageTaskStyle = st;
            try { stageTask = System.Threading.Tasks.Task.Run(() => SoftSongData(st)); } catch { stageTask = null; }
        }

        static float Hz(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);

        static float[] Bell(float f, float sec = 1.3f, float gain = 0.17f)
        {
            int n = (int)(SAMPLE_RATE * sec); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE, w = 2f * Mathf.PI * f * t;
                float v = Mathf.Sin(w) + 0.35f * Mathf.Sin(2f * w) * Mathf.Exp(-2f * t) + 0.12f * Mathf.Sin(3f * w) * Mathf.Exp(-6f * t);
                d[i] = v * Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-3.2f * t) * gain;
            }
            return d;
        }

        static float[] Marimba(float f, float sec = 0.45f, float gain = 0.05f)
        {
            int n = (int)(SAMPLE_RATE * sec); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE, w = 2f * Mathf.PI * f * t;
                d[i] = (Mathf.Sin(w) + 0.25f * Mathf.Sin(4f * w) * Mathf.Exp(-30f * t)) * Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-9f * t) * gain;
            }
            return d;
        }

        static float[] SoftBass(float f, float sec, float gain = 0.20f)
        {
            int n = (int)(SAMPLE_RATE * sec); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE, w = 2f * Mathf.PI * f * t;
                float rel = Mathf.Min(1f, (sec - t) / 0.03f);
                d[i] = (Mathf.Sin(w) + 0.35f * Mathf.Sin(2f * w)) * Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-3f * t / sec) * rel * gain;
            }
            return d;
        }

        static float[] Pad(int[] chord, float sec, float gain = 0.035f)
        {
            int n = (int)(SAMPLE_RATE * sec); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE, v = 0f;
                foreach (int m in chord) v += Mathf.Sin(2f * Mathf.PI * Hz(m) * t);
                d[i] = v * Mathf.Min(1f, t / 0.25f) * Mathf.Min(1f, (sec - t) / 0.2f) * gain;
            }
            return d;
        }

        static float[] SoftKick(float gain = 0.18f)
        {
            int n = (int)(SAMPLE_RATE * 0.12f); var d = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++) { float t = (float)i / n; ph += (110f + (50f - 110f) * t) / SAMPLE_RATE; d[i] = Mathf.Sin(2f * Mathf.PI * ph) * Mathf.Exp(-5f * t) * gain; }
            return d;
        }

        /// <summary>쉐이커(tone 0) · 틱(tone 1800Hz 섞음): 높은 소리만 남긴 잡음</summary>
        static float[] SoftNoise(float dur, float decay, float gain, int seed, float tone)
        {
            int n = (int)(SAMPLE_RATE * dur); var d = new float[n]; var rng = new System.Random(seed); float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1), hp = x - prev; prev = x;
                float t = (float)i / n;
                float v = tone > 0 ? hp * 0.6f + Mathf.Sin(2f * Mathf.PI * tone * i / SAMPLE_RATE) : hp;
                d[i] = v * Mathf.Exp(-decay * t) * gain;
            }
            return d;
        }

        class SoftSec { public int[][] chords; public int[] mel; public string drums; public bool arp = true; public float[][] bass = BassSlow; }
        // 베이스 무늬: {칸, 음 0 = 근음 · 1 = 낮은 5음 · 2 = 한 옥타브 위 근음, 길이 칸, 세기}
        static readonly float[][] BassSlow = { new[] { 0f, 0f, 4f, 0.20f }, new[] { 4f, 1f, 4f, 0.16f } };
        static readonly float[][] BassBounce = { new[] { 0f, 0f, 2f, 0.20f }, new[] { 3f, 2f, 1f, 0.10f }, new[] { 4f, 1f, 2f, 0.16f }, new[] { 6f, 2f, 1f, 0.10f } };

        static int[] Sparse(int[] m, System.Func<int, bool> keep) { var r = new int[m.Length]; for (int i = 0; i < m.Length; i++) r[i] = keep(i) ? m[i] : 0; return r; }

        /// <summary>새 평소 배경음 소리 값 (style 1 = A 봄 산책 · 2 = B 낮잠 · 3 = C 낮잠 경쾌) — 어느 스레드에서도 됨</summary>
        static float[] SoftSongData(int style)
        {
            int[] C = { 60, 64, 67 }, Am = { 57, 60, 64 }, F = { 53, 57, 60 }, G = { 55, 59, 62 }, Em = { 52, 55, 59 }, Dm = { 50, 53, 57 }, Bb = { 58, 62, 65 };
            float bpm, gain;
            SoftSec[] secs;
            if (style == 3)
            {
                int[] Ch = { 48, 52, 55 }, Amh = { 57, 60, 64 };
                int[] m1 = { 72, 0, 74, 69, 72, 0, 77, 0, 74, 0, 76, 72, 69, 0, 72, 0, 70, 0, 74, 0, 77, 0, 74, 72, 72, 0, 67, 69, 67, 0, 0, 0 };
                int[] m3 = { 74, 0, 72, 0, 70, 0, 72, 74, 72, 0, 74, 0, 76, 0, 79, 0, 76, 0, 72, 0, 69, 0, 72, 0, 74, 0, 72, 0, 69, 0, 0, 0 };
                int[] m4 = { 70, 0, 74, 0, 77, 0, 74, 0, 79, 0, 76, 0, 72, 0, 74, 76, 77, 0, 72, 0, 69, 0, 72, 0, 77, 0, 0, 0, 0, 0, 0, 0 };
                const string D = "kstsksts";
                bpm = 126f; gain = 0.531f;
                secs = new[]
                {
                    new SoftSec { chords = new[] { F, Dm, Bb, Ch }, mel = Sparse(m1, i => i % 4 == 0), drums = "k...t...", arp = false },
                    new SoftSec { chords = new[] { F, Dm, Bb, Ch }, mel = m1, drums = D, bass = BassBounce },
                    new SoftSec { chords = new[] { Bb, Ch, Amh, Dm }, mel = m3, drums = D, bass = BassBounce },
                    new SoftSec { chords = new[] { F, Dm, Bb, Ch }, mel = m1, drums = "kstskstt", bass = BassBounce },
                    new SoftSec { chords = new[] { Bb, Ch, F, F }, mel = m4, drums = D, bass = BassBounce },
                };
            }
            else if (style == 2)
            {
                int[] Ch = { 48, 52, 55 }, Amh = { 57, 60, 64 };
                int[] m1 = { 72, 0, 0, 69, 72, 0, 77, 0, 74, 0, 0, 72, 69, 0, 0, 0, 70, 0, 74, 0, 77, 0, 74, 0, 72, 0, 0, 0, 67, 0, 0, 0 };
                int[] m3 = { 74, 0, 72, 0, 70, 0, 0, 0, 72, 0, 74, 0, 76, 0, 0, 0, 76, 0, 72, 0, 69, 0, 0, 0, 74, 0, 72, 0, 69, 0, 0, 0 };
                int[] m4 = { 70, 0, 74, 0, 77, 0, 74, 0, 79, 0, 76, 0, 72, 0, 0, 0, 77, 0, 72, 0, 69, 0, 72, 0, 77, 0, 0, 0, 0, 0, 0, 0 };
                bpm = 84f; gain = 0.716f;
                secs = new[]
                {
                    new SoftSec { chords = new[] { F, Dm, Bb, Ch }, mel = Sparse(m1, i => i % 8 == 0 || i % 8 == 4), drums = "........", arp = false },
                    new SoftSec { chords = new[] { F, Dm, Bb, Ch }, mel = m1, drums = "k...t..." },
                    new SoftSec { chords = new[] { Bb, Ch, Amh, Dm }, mel = m3, drums = "k.s.t.s." },
                    new SoftSec { chords = new[] { Bb, Ch, F, F }, mel = m4, drums = "k...t..." },
                };
            }
            else
            {
                int[] m1 = { 76, 0, 79, 0, 84, 0, 79, 0, 81, 0, 79, 76, 0, 0, 72, 0, 77, 0, 81, 0, 84, 0, 81, 79, 79, 0, 74, 0, 76, 74, 0, 0 };
                int[] m3 = { 84, 0, 81, 0, 77, 0, 81, 0, 83, 0, 79, 0, 74, 0, 79, 0, 79, 0, 76, 0, 71, 0, 76, 79, 81, 0, 0, 0, 76, 0, 72, 0 };
                int[] m4 = { 81, 79, 77, 0, 81, 0, 84, 0, 83, 81, 79, 0, 86, 0, 83, 0, 84, 0, 79, 0, 76, 0, 79, 0, 84, 0, 0, 0, 0, 0, 0, 0 };
                bpm = 92f; gain = 0.525f;
                secs = new[]
                {
                    new SoftSec { chords = new[] { C, Am, F, G }, mel = Sparse(m1, i => i % 4 == 0), drums = "........", arp = false },
                    new SoftSec { chords = new[] { C, Am, F, G }, mel = m1, drums = "k.s.t.s." },
                    new SoftSec { chords = new[] { F, G, Em, Am }, mel = m3, drums = "k.s.tss." },
                    new SoftSec { chords = new[] { F, G, C, C }, mel = m4, drums = "k.s.t.s." },
                };
            }
            int m = Mathf.CeilToInt(SAMPLE_RATE * (60f / bpm / 2f));
            int steps = 0; foreach (var sc in secs) steps += sc.mel.Length;
            var data = new float[m * steps];
            var kick = SoftKick(); var shaker = SoftNoise(0.05f, 7f, 0.035f, 71, 0f); var tick = SoftNoise(0.03f, 12f, 0.05f, 73, 1800f);
            int st = 0;
            foreach (var sc in secs)
            {
                for (int b = 0; b < sc.chords.Length; b++)
                {
                    var ch = sc.chords[b];
                    int o = (st + b * 8) * m;
                    AddWrap(data, Pad(ch, 8f * m / SAMPLE_RATE), o);
                    foreach (var bp in sc.bass)
                    {
                        int w = (int)bp[1];
                        int n = w == 0 ? ch[0] - 12 : w == 1 ? ch[2] - 24 : ch[0];
                        AddWrap(data, SoftBass(Hz(n), bp[2] * m / SAMPLE_RATE, bp[3]), o + (int)bp[0] * m);
                    }
                    if (sc.arp)
                    {
                        int[] ar = { ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[1] + 12 };
                        for (int j = 0; j < 4; j++) AddWrap(data, Marimba(Hz(ar[j])), o + (1 + 2 * j) * m);
                    }
                }
                for (int k = 0; k < sc.mel.Length; k++)
                {
                    if (sc.mel[k] > 0) AddWrap(data, Bell(Hz(sc.mel[k])), (st + k) * m);
                    char d = sc.drums[k % sc.drums.Length];
                    if (d == 'k') AddWrap(data, kick, (st + k) * m);
                    else if (d == 's') AddWrap(data, shaker, (st + k) * m);
                    else if (d == 't') AddWrap(data, tick, (st + k) * m);
                }
                st += sc.mel.Length;
            }
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);   // gain = 예전 곡보다 체감 2dB 작게 (bgm_cute.py 가 잰 값)
            return data;
        }

        /// <summary>평소 배경음: Bgm_Stage_Style 0 = 예전 곡 · 1·2 = SoftSong (미리 계산해 둔 값)</summary>
        AudioClip GenerateStageBgm()
        {
            int st = StageStyle;
            if (st > 0)
            {
                float[] d = null;
                try { if (stageTask != null && stageTaskStyle == st) d = stageTask.Result; } catch { d = null; }
                stageTask = null;
                return MakeSongClip("Stage_BGM", d ?? SoftSongData(st));
            }
            return OldStageBgm();
        }

        /// <summary>예전 메인 스테이지 40초 루프 (120 BPM, A단조 · 5구간: 처음 → 드럼 → 새 진행 → 높은 변주 → 쉬어 가기) — Bgm_Stage_Style 0</summary>
        AudioClip OldStageBgm()
        {
            float[] bassA = Cat(R(110f, 8), R(87.31f, 8), R(130.81f, 8), R(98f, 4), R(82.41f, 4));             // Am F C G-E
            float[] leadA = {
                440f, 523.25f, 659.25f, 523.25f, 440f, 523.25f, 659.25f, 587.33f,
                349.23f, 440f, 523.25f, 440f, 349.23f, 440f, 523.25f, 659.25f,
                523.25f, 659.25f, 783.99f, 659.25f, 523.25f, 659.25f, 587.33f, 523.25f,
                392.00f, 493.88f, 587.33f, 493.88f, 329.63f, 493.88f, 440f, 329.63f
            };
            float[] bassB = Cat(R(73.42f, 8), R(110f, 8), R(87.31f, 8), R(82.41f, 8));                         // Dm Am F E
            float[] leadB = {
                587.33f, 0f, 698.46f, 0f, 880f, 698.46f, 587.33f, 0f,
                659.25f, 0f, 523.25f, 0f, 440f, 523.25f, 659.25f, 0f,
                698.46f, 0f, 880f, 0f, 1046.5f, 880f, 698.46f, 0f,
                659.25f, 0f, 830.61f, 0f, 987.77f, 830.61f, 659.25f, 0f
            };
            float[] leadA2 = new float[32];   // 높은 변주: 한 옥타브 위, 박자마다 한 음씩 (사이는 쉼)
            for (int i = 0; i < 32; i++) leadA2[i] = i % 2 == 0 ? leadA[i] * 2f : 0f;
            float[] bassC = new float[32];    // 쉬어 가기: 베이스는 박자마다만, 리드는 마디 첫 음만 길게
            float[] leadC = new float[32];
            for (int i = 0; i < 32; i++) { bassC[i] = i % 2 == 0 ? bassA[i] : 0f; leadC[i] = i % 8 == 0 ? leadA[i] : 0f; }

            var sections = new[]
            {
                new Section(bassA, leadA, DrumLight),
                new Section(bassA, leadA, DrumFull),
                new Section(bassB, leadB, DrumFull),
                new Section(bassA, leadA2, DrumDrive),
                new Section(bassC, leadC, DrumHalf, 1.2f),
            };
            return BuildSongClip("Stage_BGM", 60f / 120f / 2f, sections, WaveType.Triangle);
        }

        // 11-08: 보스·던전 곡(38초)을 처음 보스·던전에 들어가는 순간 계산하면 그 프레임이 멈칫함
        //        → 켤 때 따로 일꾼(스레드)이 소리 값만 미리 계산 · 소리 조각(AudioClip)은 쓸 때 만듦
        System.Threading.Tasks.Task<float[]> bossTask;
        void PrepareBossBgm()
        {
            if (bossBgm != null || bossTask != null) return;
            try { bossTask = System.Threading.Tasks.Task.Run(BossBgmData); } catch { bossTask = null; }
        }
        AudioClip TakeBossBgm()
        {
            float[] d = null;
            try { if (bossTask != null) d = bossTask.Result; } catch { d = null; }
            bossTask = null;
            return MakeSongClip("Boss_BGM", d ?? BossBgmData());
        }

        /// <summary>보스전·던전 38.4초 루프 (150 BPM, 긴박한 단조 · 6구간) — 소리 값만 (어느 스레드에서도 됨)</summary>
        static float[] BossBgmData()
        {
            float[] bassA = {
                110f, 110f, 110f, 110f, 110f, 110f, 130.81f, 110f,
                103.83f, 103.83f, 103.83f, 103.83f, 103.83f, 103.83f, 123.47f, 103.83f,
                98.00f, 98.00f, 98.00f, 98.00f, 98.00f, 98.00f, 116.54f, 98.00f,
                82.41f, 82.41f, 87.31f, 87.31f, 98.00f, 98.00f, 82.41f, 82.41f
            };
            float[] leadA = {
                440f, 659.25f, 523.25f, 659.25f, 440f, 659.25f, 587.33f, 523.25f,
                415.30f, 659.25f, 493.88f, 659.25f, 415.30f, 659.25f, 587.33f, 493.88f,
                392.00f, 587.33f, 466.16f, 587.33f, 392.00f, 587.33f, 523.25f, 466.16f,
                329.63f, 493.88f, 349.23f, 523.25f, 392.00f, 587.33f, 493.88f, 329.63f
            };
            float[] leadA2 = new float[32];   // 한 옥타브 위 엇박
            for (int i = 0; i < 32; i++) leadA2[i] = i % 2 == 1 ? leadA[i] * 2f : 0f;
            float[] bassB = Cat(R(110f, 8), R(116.54f, 8), R(110f, 8), R(82.41f, 8));                          // Am Bb Am E
            float[] leadB = {
                880f, 830.61f, 880f, 0f, 659.25f, 0f, 880f, 0f,
                932.33f, 880f, 932.33f, 0f, 698.46f, 0f, 932.33f, 0f,
                880f, 830.61f, 880f, 0f, 1046.5f, 0f, 880f, 0f,
                830.61f, 0f, 659.25f, 0f, 493.88f, 0f, 415.30f, 0f
            };
            float[] bassC = new float[32];    // 숨 고르기: 베이스 두 박마다, 리드는 끝에서 올라가는 음
            float[] leadC = new float[32];
            float[] run = { 440f, 493.88f, 523.25f, 587.33f, 659.25f, 698.46f, 830.61f, 880f };
            for (int i = 0; i < 32; i++) { bassC[i] = i % 4 == 0 ? bassA[i] : 0f; leadC[i] = i >= 24 ? run[i - 24] : 0f; }

            var sections = new[]
            {
                new Section(bassA, leadA, DrumFull),
                new Section(bassA, leadA2, DrumDrive),
                new Section(bassB, leadB, DrumFull),
                new Section(bassA, leadA, DrumDrive),
                new Section(bassC, leadC, DrumHalf),
                new Section(bassA, leadA, DrumFull),
            };
            return BuildSongData(60f / 150f / 2f, sections, WaveType.Square);
        }

        static AudioClip BuildSongClip(string name, float stepDur, Section[] sections, WaveType leadWave) => MakeSongClip(name, BuildSongData(stepDur, sections, leadWave));

        static AudioClip MakeSongClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] BuildSongData(float stepDur, Section[] sections, WaveType leadWave)
        {
            int stepSamples = Mathf.CeilToInt(SAMPLE_RATE * stepDur);
            int steps = 0;
            foreach (var s in sections) steps += s.bass.Length;
            int total = stepSamples * steps;
            float[] data = new float[total];
            float bassPhase = 0f, leadPhase = 0f;
            int envFade = Mathf.Min(stepSamples / 4, Mathf.CeilToInt(SAMPLE_RATE * 0.002f));

            var kick = Thump(0.14f, 150f, 45f, 0.32f);
            var snare = Mix(NoiseBurst(0.12f, 28f, 0.16f, false, 31), Sweep(200f, 160f, 0.06f, WaveType.Triangle, 0.08f));
            var hat = NoiseBurst(0.04f, 90f, 0.10f, true, 57);

            int step = 0;
            foreach (var sec in sections)
            {
                for (int k = 0; k < sec.bass.Length; k++, step++)
                {
                    int o = step * stepSamples;
                    float bf = sec.bass[k], lf = sec.lead[k];
                    for (int i = 0; i < stepSamples; i++)
                    {
                        float t = (float)i / stepSamples;
                        float edge = i < envFade ? (float)i / envFade : (i > stepSamples - envFade ? (float)(stepSamples - i) / envFade : 1f);
                        float v = 0f;
                        if (bf > 0f) { bassPhase += bf / SAMPLE_RATE; v += SampleWave(bassPhase, WaveType.Saw) * Mathf.Exp(-3.5f * t) * 0.30f * edge; }
                        if (lf > 0f) { leadPhase += lf / SAMPLE_RATE; v += SampleWave(leadPhase, leadWave) * Mathf.Exp(-sec.leadDecay * t) * 0.20f * edge; }
                        data[o + i] += v;
                    }
                    char d = sec.drums[k % sec.drums.Length];
                    if (d == 'k') AddWrap(data, kick, o);
                    else if (d == 's') AddWrap(data, snare, o);
                    else if (d == 'h') AddWrap(data, hat, o);
                }
            }
            for (int i = 0; i < total; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            return data;
        }

        /// <summary>드럼 꼬리가 곡 끝을 넘으면 처음으로 돌려 붙임 (반복 이음새가 끊기지 않게)</summary>
        static void AddWrap(float[] data, float[] src, int at)
        {
            for (int i = 0; i < src.Length; i++) data[(at + i) % data.Length] += src[i];
        }

        static float SampleWave(float phase, WaveType type)
        {
            float p = phase - Mathf.Floor(phase);
            return type switch
            {
                WaveType.Sine => Mathf.Sin(p * Mathf.PI * 2f),
                WaveType.Square => p < 0.5f ? 1f : -1f,
                WaveType.Saw => 2f * p - 1f,
                WaveType.Triangle => 1f - 4f * Mathf.Abs(Mathf.Round(p - 0.25f) - (p - 0.25f)),
                _ => 0f
            };
        }
    }
}
