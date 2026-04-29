using System.Collections.Generic;
using UnityEngine;

namespace Breathe.Gameplay
{
    // Handles the visual scene and stone-skipping physics for the Stone Skip minigame.
    // Builds the lake, shore, character, and manages the stone's flight and skip arcs.
    public class StoneSkipController : MonoBehaviour
    {
        [Header("Lake")]
        [SerializeField] private int _waveLineCount = 15;
        [SerializeField] private Color _waterColor = new Color(0.12f, 0.35f, 0.65f);
        [SerializeField] private Color _waveLineColor = new Color(0.25f, 0.50f, 0.78f, 0.4f);

        [Header("Stone Physics")]
        [SerializeField] private float _baseThrowSpeed = 12f;
        [SerializeField] private float _gravity = 14f;
        [SerializeField] private float _bounceDecay = 0.55f;
        [SerializeField] private float _minBounceVelocity = 1.35f;
        [SerializeField] private float _skipHeight = 2.5f;
        [SerializeField] private float _waterLevel = -1.8f;

        [Header("Character")]
        [SerializeField] private float _characterX = -7f;

        private enum StoneState { Idle, Flying, Skipping, Sinking, Done }

        private StoneState _stoneState = StoneState.Idle;
        private Vector2 _stonePos;
        private Vector2 _stoneVel;
        private float _currentBounceVel;
        private int _skipCount;
        private float _distanceTraveled;
        private float _sinkTimer;

        // Scene objects
        private GameObject _stoneObj;
        private SpriteRenderer _stoneRenderer;
        private LineRenderer[] _waveLines;
        private LineRenderer _shoreLine;
        private GameObject _characterBody;
        private LineRenderer[] _characterLines;
        private float _windUpProgress;
        private List<SplashEffect> _splashes = new();

        // Splash pool
        private struct SplashEffect
        {
            public GameObject Obj;
            public SpriteRenderer Renderer;
            public float Timer;
            public float Duration;
        }

        private static Material _spriteMat;
        private Texture2D _circleTex;
        private Texture2D _stoneTex;

        public int SkipCount => _skipCount;
        public float DistanceTraveled => _distanceTraveled;
        public bool IsStoneActive => _stoneState == StoneState.Flying || _stoneState == StoneState.Skipping;
        public bool IsRoundDone => _stoneState == StoneState.Done;

        public void Initialize()
        {
            EnsureMaterial();
            _circleTex = GenerateCircleTexture(64);
            _stoneTex = GenerateCircleTexture(32);

            BuildBackground();
            BuildLake();
            BuildShore();
            BuildCharacter();
            BuildStone();
        }

        // Start the wind-up animation. Called each frame during the blow phase.
        public void SetWindUpProgress(float progress)
        {
            _windUpProgress = Mathf.Clamp01(progress);
            UpdateCharacterPose();
        }

        public void LaunchStone(float throwPower)
        {
            _stoneState = StoneState.Flying;
            _skipCount = 0;
            _distanceTraveled = 0f;

            float speed = _baseThrowSpeed * Mathf.Lerp(0.3f, 1f, throwPower);
            float angle = Mathf.Lerp(15f, 8f, throwPower) * Mathf.Deg2Rad;

            _stonePos = new Vector2(_characterX + 1.5f, 0.5f);
            _stoneVel = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
            _currentBounceVel = _skipHeight * Mathf.Lerp(0.5f, 1f, throwPower);

            _stoneObj.SetActive(true);
            _stoneObj.transform.position = new Vector3(_stonePos.x, _stonePos.y, -1f);
            _windUpProgress = 0f;
            UpdateCharacterPose();
        }

        public void ResetForNewRound()
        {
            _stoneState = StoneState.Idle;
            _stoneObj.SetActive(false);
            _skipCount = 0;
            _distanceTraveled = 0f;
            _windUpProgress = 0f;
            UpdateCharacterPose();
        }

        public void UpdateStone()
        {
            float dt = Time.deltaTime;

            switch (_stoneState)
            {
                case StoneState.Flying:
                case StoneState.Skipping:
                    _stoneVel.y -= _gravity * dt;
                    _stonePos += _stoneVel * dt;

                    // Hit water surface
                    if (_stonePos.y <= _waterLevel)
                    {
                        _stonePos.y = _waterLevel;
                        SpawnSplash(_stonePos);
                        _skipCount++;

                        _distanceTraveled = _stonePos.x - (_characterX + 1.5f);

                        // Bounce with decay
                        _currentBounceVel *= _bounceDecay;

                        if (_currentBounceVel < _minBounceVelocity)
                        {
                            _stoneState = StoneState.Sinking;
                            _sinkTimer = 0f;
                            _stoneVel = Vector2.zero;
                        }
                        else
                        {
                            _stoneVel.y = _currentBounceVel;
                            _stoneVel.x *= 0.85f;
                            _stoneState = StoneState.Skipping;
                        }
                    }

                    _stoneObj.transform.position = new Vector3(_stonePos.x, _stonePos.y, -1f);

                    // Stone rotation during flight
                    float rotSpeed = _stoneVel.magnitude * 15f;
                    _stoneObj.transform.Rotate(0f, 0f, -rotSpeed * dt);
                    break;

                case StoneState.Sinking:
                    _sinkTimer += dt;
                    _stonePos.y -= dt * 0.8f;
                    _stoneObj.transform.position = new Vector3(_stonePos.x, _stonePos.y, -1f);

                    float sinkAlpha = Mathf.Clamp01(1f - _sinkTimer / 1.5f);
                    _stoneRenderer.color = new Color(0.4f, 0.4f, 0.45f, sinkAlpha);

                    if (_sinkTimer > 1.5f)
                    {
                        _stoneState = StoneState.Done;
                        _stoneObj.SetActive(false);
                    }
                    break;
            }

            UpdateSplashes(dt);
            AnimateWaves();
        }

        private void SpawnSplash(Vector2 pos)
        {
            // Create or reuse a splash
            var splashObj = new GameObject("Splash");
            splashObj.transform.SetParent(transform);
            var sr = splashObj.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(_circleTex,
                new Rect(0, 0, _circleTex.width, _circleTex.height),
                new Vector2(0.5f, 0.5f), 32f);
            sr.color = new Color(0.7f, 0.85f, 1f, 0.8f);
            sr.sortingOrder = 5;
            splashObj.transform.position = new Vector3(pos.x, pos.y, -0.5f);
            splashObj.transform.localScale = Vector3.one * 0.3f;

            _splashes.Add(new SplashEffect
            {
                Obj = splashObj,
                Renderer = sr,
                Timer = 0f,
                Duration = 0.6f
            });
        }

        private void UpdateSplashes(float dt)
        {
            for (int i = _splashes.Count - 1; i >= 0; i--)
            {
                var s = _splashes[i];
                s.Timer += dt;
                _splashes[i] = s;

                float t = s.Timer / s.Duration;
                float scale = 0.3f + t * 1.2f;
                s.Obj.transform.localScale = new Vector3(scale, scale * 0.5f, 1f);
                s.Renderer.color = new Color(0.7f, 0.85f, 1f, 0.8f * (1f - t));

                if (t >= 1f)
                {
                    Destroy(s.Obj);
                    _splashes.RemoveAt(i);
                }
            }
        }

        private void BuildBackground()
        {
            var cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = new Color(0.45f, 0.72f, 0.92f);

            // Sky gradient
            var skyObj = new GameObject("Sky");
            skyObj.transform.SetParent(transform);
            var skyRend = skyObj.AddComponent<SpriteRenderer>();
            var skyTex = GenerateGradientTexture(4, 64,
                new Color(0.55f, 0.78f, 0.92f),
                new Color(0.35f, 0.58f, 0.88f));
            skyRend.sprite = Sprite.Create(skyTex, new Rect(0, 0, skyTex.width, skyTex.height),
                new Vector2(0.5f, 0.5f), 8f);
            skyRend.sortingOrder = -30;
            skyObj.transform.position = new Vector3(0f, 3f, 10f);
            skyObj.transform.localScale = new Vector3(40f, 15f, 1f);
        }

        private void BuildLake()
        {
            // Water fill
            var waterObj = new GameObject("Water");
            waterObj.transform.SetParent(transform);
            var waterRend = waterObj.AddComponent<SpriteRenderer>();
            var waterTex = GenerateGradientTexture(4, 32,
                new Color(0.06f, 0.18f, 0.40f),
                _waterColor);
            waterRend.sprite = Sprite.Create(waterTex, new Rect(0, 0, waterTex.width, waterTex.height),
                new Vector2(0.5f, 1f), 8f);
            waterRend.sortingOrder = -10;
            waterObj.transform.position = new Vector3(2f, _waterLevel, 5f);
            waterObj.transform.localScale = new Vector3(30f, 8f, 1f);

            // Wave lines
            _waveLines = new LineRenderer[_waveLineCount];
            var wavesParent = new GameObject("Waves");
            wavesParent.transform.SetParent(transform);

            for (int i = 0; i < _waveLineCount; i++)
            {
                var waveObj = new GameObject($"Wave_{i}");
                waveObj.transform.SetParent(wavesParent.transform);
                var lr = waveObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 60;
                lr.material = _spriteMat;
                lr.startColor = _waveLineColor;
                lr.endColor = _waveLineColor;
                lr.widthMultiplier = 0.04f;
                lr.sortingOrder = -5;
                lr.numCornerVertices = 4;
                _waveLines[i] = lr;
            }
        }

        private void AnimateWaves()
        {
            if (_waveLines == null) return;

            float t = Time.time;
            for (int i = 0; i < _waveLines.Length; i++)
            {
                float y = _waterLevel - i * 0.4f;
                float phase = i * 0.7f;
                float amp = 0.12f * (1f - i * 0.04f);

                for (int s = 0; s < 60; s++)
                {
                    float x = -10f + s * 0.5f;
                    float wy = y + Mathf.Sin(x * 0.5f + t * 1.5f + phase) * amp
                                 + Mathf.Sin(x * 0.3f + t * 0.8f + phase * 0.5f) * amp * 0.5f;
                    _waveLines[i].SetPosition(s, new Vector3(x, wy, 0f));
                }
            }
        }

        private void BuildShore()
        {
            var shoreObj = new GameObject("Shore");
            shoreObj.transform.SetParent(transform);
            var shoreRend = shoreObj.AddComponent<SpriteRenderer>();
            var shoreTex = GenerateSolidTexture(4, 4, new Color(0.65f, 0.55f, 0.35f));
            shoreRend.sprite = Sprite.Create(shoreTex, new Rect(0, 0, shoreTex.width, shoreTex.height),
                new Vector2(1f, 0.5f), 4f);
            shoreRend.sortingOrder = -8;
            shoreObj.transform.position = new Vector3(_characterX + 1f, _waterLevel - 1f, 2f);
            shoreObj.transform.localScale = new Vector3(5f, 5f, 1f);

            // Shore edge (transition to water)
            var edgeObj = new GameObject("ShoreEdge");
            edgeObj.transform.SetParent(transform);
            _shoreLine = edgeObj.AddComponent<LineRenderer>();
            _shoreLine.useWorldSpace = true;
            _shoreLine.positionCount = 20;
            _shoreLine.material = _spriteMat;
            _shoreLine.startColor = new Color(0.55f, 0.45f, 0.30f);
            _shoreLine.endColor = new Color(0.55f, 0.45f, 0.30f);
            _shoreLine.widthMultiplier = 0.15f;
            _shoreLine.sortingOrder = -7;

            for (int i = 0; i < 20; i++)
            {
                float frac = i / 19f;
                float y = _waterLevel + Mathf.Lerp(-3.5f, 0.5f, frac);
                float x = _characterX + 1.2f + Mathf.Sin(frac * Mathf.PI * 0.5f) * 1.5f;
                _shoreLine.SetPosition(i, new Vector3(x, y, 1f));
            }
        }

        private void BuildCharacter()
        {
            _characterBody = new GameObject("Character");
            _characterBody.transform.SetParent(transform);
            _characterBody.transform.position = new Vector3(_characterX, _waterLevel + 0.3f, -0.5f);

            // Stick figure using LineRenderers
            _characterLines = new LineRenderer[5];
            string[] names = { "Body", "LeftLeg", "RightLeg", "LeftArm", "RightArm" };

            for (int i = 0; i < 5; i++)
            {
                var obj = new GameObject(names[i]);
                obj.transform.SetParent(_characterBody.transform);
                var lr = obj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.material = _spriteMat;
                lr.startColor = new Color(0.15f, 0.15f, 0.2f);
                lr.endColor = new Color(0.15f, 0.15f, 0.2f);
                lr.widthMultiplier = 0.1f;
                lr.sortingOrder = 2;
                lr.numCornerVertices = 3;
                _characterLines[i] = lr;
            }

            // Head
            var headObj = new GameObject("Head");
            headObj.transform.SetParent(_characterBody.transform);
            var headRend = headObj.AddComponent<SpriteRenderer>();
            headRend.sprite = Sprite.Create(_circleTex,
                new Rect(0, 0, _circleTex.width, _circleTex.height),
                new Vector2(0.5f, 0.5f), 64f);
            headRend.color = new Color(0.15f, 0.15f, 0.2f);
            headRend.sortingOrder = 2;
            headObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            headObj.transform.localScale = Vector3.one * 0.6f;

            UpdateCharacterPose();
        }

        private void UpdateCharacterPose()
        {
            if (_characterLines == null) return;

            // Wind-up: arm goes back, body leans forward
            float lean = _windUpProgress * 15f;
            _characterBody.transform.rotation = Quaternion.Euler(0f, 0f, lean);

            // Body
            _characterLines[0].positionCount = 2;
            _characterLines[0].SetPosition(0, new Vector3(0f, 0.5f, 0f));
            _characterLines[0].SetPosition(1, new Vector3(0f, 2.2f, 0f));

            // Left leg
            _characterLines[1].positionCount = 3;
            _characterLines[1].SetPosition(0, new Vector3(0f, 0.5f, 0f));
            _characterLines[1].SetPosition(1, new Vector3(-0.25f, -0.1f, 0f));
            _characterLines[1].SetPosition(2, new Vector3(-0.3f, -0.7f, 0f));

            // Right leg
            _characterLines[2].positionCount = 3;
            _characterLines[2].SetPosition(0, new Vector3(0f, 0.5f, 0f));
            _characterLines[2].SetPosition(1, new Vector3(0.25f, -0.1f, 0f));
            _characterLines[2].SetPosition(2, new Vector3(0.3f, -0.7f, 0f));

            // Right arm (throwing arm) — swings back during wind-up
            float armAngle = Mathf.Lerp(0f, -70f, _windUpProgress) * Mathf.Deg2Rad;
            float armLen = 0.8f;
            _characterLines[4].positionCount = 2;
            _characterLines[4].SetPosition(0, new Vector3(0f, 1.8f, 0f));
            _characterLines[4].SetPosition(1, new Vector3(
                Mathf.Sin(armAngle) * armLen,
                1.8f + Mathf.Cos(armAngle) * armLen, 0f));

            // Left arm (stays at side)
            _characterLines[3].positionCount = 2;
            _characterLines[3].SetPosition(0, new Vector3(0f, 1.8f, 0f));
            _characterLines[3].SetPosition(1, new Vector3(-0.4f, 1.2f, 0f));
        }

        private void BuildStone()
        {
            _stoneObj = new GameObject("Stone");
            _stoneObj.transform.SetParent(transform);
            _stoneRenderer = _stoneObj.AddComponent<SpriteRenderer>();
            _stoneRenderer.sprite = Sprite.Create(_stoneTex,
                new Rect(0, 0, _stoneTex.width, _stoneTex.height),
                new Vector2(0.5f, 0.5f), 64f);
            _stoneRenderer.color = new Color(0.4f, 0.4f, 0.45f);
            _stoneRenderer.sortingOrder = 10;
            _stoneObj.transform.localScale = Vector3.one * 0.8f;
            _stoneObj.SetActive(false);
        }

        private static void EnsureMaterial()
        {
            if (_spriteMat != null) return;
            _spriteMat = new Material(Shader.Find("Sprites/Default"));
        }

        private static Texture2D GenerateCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((radius - dist) * 2f)));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateGradientTexture(int w, int h, Color bottom, Color top)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / h);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateSolidTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }
    }
}
