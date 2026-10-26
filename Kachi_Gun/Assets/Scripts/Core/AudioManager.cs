using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 사운드 매니저: 외부 음원 파일 없이 코드로 직접 합성(Procedural)하여 배경음(BGM 2종)과 효과음(SFX 16종)을 재생한다.
    /// 씬에 없어도 게임 시작 시 자동 생성되며(DontDestroyOnLoad), 설정 창의 배경음·효과음 켜기/끄기(SaveData.bgm / sfx)와 즉시 연동된다.
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
            Coin,        // 골드·다이아 획득 (연속 획득 시 피치 상승)
            UiClick,     // UI 버튼 클릭
            Victory,     // 던전 승리
            Fail         // 보스 실패 · 던전 실패 · 슬롯 강화 실패
        }

        [Range(0f, 1f)] public float bgmVolume = 0.22f;
        [Range(0f, 1f)] public float sfxVolume = 0.48f;
        public int channels = 16;

        AudioSource bgmPlayer;
        AudioLowPassFilter bgmFilter;
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
            if (I != null && I.bgmFilter != null) I.bgmFilter.enabled = on;
        }

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

            var bgmObj = new GameObject("BgmPlayer");
            bgmObj.transform.SetParent(transform, false);
            bgmPlayer = bgmObj.AddComponent<AudioSource>();
            bgmPlayer.playOnAwake = false;
            bgmPlayer.loop = true;
            bgmPlayer.volume = bgmVolume;

            bgmFilter = bgmObj.AddComponent<AudioLowPassFilter>();
            bgmFilter.cutoffFrequency = 800f;
            bgmFilter.enabled = false;

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
            ApplySettings();
        }

        public void ApplySettings()
        {
            bool bgmOn = S == null || S.bgm;
            bool sfxOn = S == null || S.sfx;

            if (!bgmOn)
            {
                bgmPlayer.Stop();
            }
            else
            {
                UpdateBgmClip();
            }

            if (!sfxOn && sfxPlayers != null)
            {
                foreach (var p in sfxPlayers) p.Stop();
            }
        }

        public void UpdateBgmClip()
        {
            if (bgmPlayer == null) return;
            if (S != null && !S.bgm)
            {
                bgmPlayer.Stop();
                return;
            }

            var gm = GameManager.I;
            bool intense = gm != null && (gm.bossPhase || gm.mode != GameManager.Mode.Stage);
            AudioClip target = intense ? (bossBgm ??= GenerateBossBgm()) : (stageBgm ??= GenerateStageBgm());

            if (bgmPlayer.isPlaying && bgmPlayer.clip == target) return;
            bgmPlayer.clip = target;
            bgmPlayer.volume = bgmVolume;
            bgmPlayer.Play();
        }

        public void PlaySfx(Sfx sfx)
        {
            if (S != null && !S.sfx) return;
            if (GameManager.I != null && GameManager.I.eco && sfx != Sfx.UiClick) return; // 절전모드 중에는 전투 효과음 생략

            float now = Time.unscaledTime;
            float minInterval = sfx switch
            {
                Sfx.Shot or Sfx.Shotgun or Sfx.Laser or Sfx.Missile => 0.045f,
                Sfx.Hit or Sfx.MineHit => 0.045f,
                Sfx.Dead or Sfx.Coin => 0.04f,
                Sfx.Boom => 0.06f,
                _ => 0.02f
            };
            if (lastPlayTime.TryGetValue(sfx, out float last) && now - last < minInterval) return;
            lastPlayTime[sfx] = now;

            if (!sfxClips.TryGetValue(sfx, out AudioClip clip) || clip == null) return;

            for (int i = 0; i < sfxPlayers.Length; i++)
            {
                int idx = (i + channelIndex) % sfxPlayers.Length;
                if (sfxPlayers[idx].isPlaying) continue;

                channelIndex = idx;
                var player = sfxPlayers[idx];
                player.clip = clip;
                player.volume = sfxVolume;

                if (sfx == Sfx.Coin)
                {
                    coinPitch = now - lastCoinTime < 0.45f ? Mathf.Min(coinPitch + 0.04f, 1.55f) : 1.0f;
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
                break;
            }
        }

        // ---------- 버튼 클릭 소리: 누른 버튼과 뗀 자리 버튼이 같을 때 ----------
        void Update()
        {
            if (Input.GetMouseButtonDown(0)) pressed = SelectableUnder(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0))
            {
                if (pressed != null && pressed == SelectableUnder(Input.mousePosition))
                    PlaySfx(Sfx.UiClick);
                pressed = null;
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
        // =========================================================================
        void GenerateAllProceduralSounds()
        {
            sfxClips[Sfx.Shot] = CreateSweepWithNoise("Sfx_Shot", 520f, 120f, 0.075f, 0.45f, 0.55f);
            sfxClips[Sfx.Shotgun] = CreateSweepWithNoise("Sfx_Shotgun", 340f, 70f, 0.13f, 0.65f, 0.70f);
            sfxClips[Sfx.Laser] = CreateSweep("Sfx_Laser", 1350f, 260f, 0.095f, WaveType.Saw, 0.45f);
            sfxClips[Sfx.Missile] = CreateSweepWithNoise("Sfx_Missile", 280f, 95f, 0.16f, 0.55f, 0.55f);
            sfxClips[Sfx.Boom] = CreateSweepWithNoise("Sfx_Boom", 160f, 38f, 0.28f, 0.70f, 0.85f);
            sfxClips[Sfx.Hit] = CreateSweepWithNoise("Sfx_Hit", 210f, 65f, 0.065f, 0.38f, 0.55f);
            sfxClips[Sfx.MineHit] = CreateArpeggio("Sfx_MineHit", new[] { 1174.66f, 1567.98f }, 0.032f, WaveType.Triangle, 0.45f);
            sfxClips[Sfx.Dead] = CreateSweep("Sfx_Dead", 240f, 480f, 0.08f, WaveType.Triangle, 0.42f);
            sfxClips[Sfx.BossAppear] = CreateArpeggio("Sfx_BossAppear", new[] { 220f, 207.65f, 196f, 185f, 220f }, 0.11f, WaveType.Saw, 0.6f);
            sfxClips[Sfx.BossDead] = CreateArpeggio("Sfx_BossDead", new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, WaveType.Square, 0.6f);
            sfxClips[Sfx.Upgrade] = CreateArpeggio("Sfx_Upgrade", new[] { 523.25f, 659.25f, 783.99f }, 0.045f, WaveType.Triangle, 0.48f);
            sfxClips[Sfx.Merge] = CreateArpeggio("Sfx_Merge", new[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.075f, WaveType.Square, 0.5f);
            sfxClips[Sfx.Coin] = CreateArpeggio("Sfx_Coin", new[] { 987.77f, 1318.51f }, 0.042f, WaveType.Sine, 0.45f);
            sfxClips[Sfx.UiClick] = CreateArpeggio("Sfx_UiClick", new[] { 659.25f, 880f }, 0.035f, WaveType.Triangle, 0.38f);
            sfxClips[Sfx.Victory] = CreateArpeggio("Sfx_Victory", new[] { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f }, 0.10f, WaveType.Square, 0.52f);
            sfxClips[Sfx.Fail] = CreateArpeggio("Sfx_Fail", new[] { 392.00f, 349.23f, 311.13f, 261.63f }, 0.15f, WaveType.Saw, 0.5f);
        }

        enum WaveType { Sine, Square, Saw, Triangle }

        static AudioClip MakeClip(string name, float[] data)
        {
            int fade = Mathf.Min(data.Length, Mathf.CeilToInt(SAMPLE_RATE * FadeInSec));
            for (int i = 0; i < fade; i++) data[i] *= (float)i / fade;
            int fadeOut = Mathf.Min(data.Length, Mathf.CeilToInt(SAMPLE_RATE * 0.003f));
            for (int i = 0; i < fadeOut; i++) data[data.Length - 1 - i] *= (float)i / fadeOut;
            var clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip CreateSweep(string name, float startFreq, float endFreq, float duration, WaveType type, float gain)
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
            return MakeClip(name, data);
        }

        AudioClip CreateSweepWithNoise(string name, float startFreq, float endFreq, float duration, float noiseRatio, float gain)
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
            return MakeClip(name, data);
        }

        AudioClip CreateArpeggio(string name, float[] notes, float stepDuration, WaveType type, float gain)
        {
            int stepSamples = Mathf.CeilToInt(SAMPLE_RATE * stepDuration);
            float[] data = new float[stepSamples * notes.Length];
            float phase = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                for (int i = 0; i < stepSamples; i++)
                {
                    float t = (float)i / stepSamples;
                    phase += notes[n] / SAMPLE_RATE;
                    data[n * stepSamples + i] = SampleWave(phase, type) * Mathf.Exp(-2.5f * t) * gain;
                }
            }
            return MakeClip(name, data);
        }

        /// <summary>메인 스테이지 8초 루프 BGM (120 BPM, A단조 — 베이스 톱니파 + 리드 삼각파 아르페지오)</summary>
        AudioClip GenerateStageBgm()
        {
            float stepDur = 60f / 120f / 2f;
            float[] bassNotes = {
                110f, 110f, 110f, 110f, 110f, 110f, 110f, 110f,                         // A2
                87.31f, 87.31f, 87.31f, 87.31f, 87.31f, 87.31f, 87.31f, 87.31f,         // F2
                130.81f, 130.81f, 130.81f, 130.81f, 130.81f, 130.81f, 130.81f, 130.81f, // C3
                98.00f, 98.00f, 98.00f, 98.00f, 82.41f, 82.41f, 82.41f, 82.41f          // G2 -> E2
            };
            float[] leadNotes = {
                440f, 523.25f, 659.25f, 523.25f, 440f, 523.25f, 659.25f, 587.33f,
                349.23f, 440f, 523.25f, 440f, 349.23f, 440f, 523.25f, 659.25f,
                523.25f, 659.25f, 783.99f, 659.25f, 523.25f, 659.25f, 587.33f, 523.25f,
                392.00f, 493.88f, 587.33f, 493.88f, 329.63f, 493.88f, 440f, 329.63f
            };
            return BuildLoopClip("Stage_BGM", stepDur, bassNotes, leadNotes, WaveType.Triangle);
        }

        /// <summary>보스전·던전 6.4초 루프 BGM (150 BPM, 긴박한 단조 진행)</summary>
        AudioClip GenerateBossBgm()
        {
            float stepDur = 60f / 150f / 2f;
            float[] bassNotes = {
                110f, 110f, 110f, 110f, 110f, 110f, 130.81f, 110f,
                103.83f, 103.83f, 103.83f, 103.83f, 103.83f, 103.83f, 123.47f, 103.83f,
                98.00f, 98.00f, 98.00f, 98.00f, 98.00f, 98.00f, 116.54f, 98.00f,
                82.41f, 82.41f, 87.31f, 87.31f, 98.00f, 98.00f, 82.41f, 82.41f
            };
            float[] leadNotes = {
                440f, 659.25f, 523.25f, 659.25f, 440f, 659.25f, 587.33f, 523.25f,
                415.30f, 659.25f, 493.88f, 659.25f, 415.30f, 659.25f, 587.33f, 493.88f,
                392.00f, 587.33f, 466.16f, 587.33f, 392.00f, 587.33f, 523.25f, 466.16f,
                329.63f, 493.88f, 349.23f, 523.25f, 392.00f, 587.33f, 493.88f, 329.63f
            };
            return BuildLoopClip("Boss_BGM", stepDur, bassNotes, leadNotes, WaveType.Square);
        }

        static AudioClip BuildLoopClip(string name, float stepDur, float[] bassNotes, float[] leadNotes, WaveType leadWave)
        {
            int stepSamples = Mathf.CeilToInt(SAMPLE_RATE * stepDur);
            float[] data = new float[stepSamples * bassNotes.Length];
            float bassPhase = 0f, leadPhase = 0f;
            int envFade = Mathf.Min(stepSamples / 4, Mathf.CeilToInt(SAMPLE_RATE * 0.002f));
            for (int step = 0; step < bassNotes.Length; step++)
            {
                for (int i = 0; i < stepSamples; i++)
                {
                    float t = (float)i / stepSamples;
                    bassPhase += bassNotes[step] / SAMPLE_RATE;
                    leadPhase += leadNotes[step] / SAMPLE_RATE;
                    float edge = i < envFade ? (float)i / envFade : (i > stepSamples - envFade ? (float)(stepSamples - i) / envFade : 1f);
                    float bass = SampleWave(bassPhase, WaveType.Saw) * Mathf.Exp(-3.5f * t) * 0.32f * edge;
                    float lead = SampleWave(leadPhase, leadWave) * Mathf.Exp(-4.2f * t) * 0.22f * edge;
                    data[step * stepSamples + i] = Mathf.Clamp(bass + lead, -1f, 1f);
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
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
