using System;
using UnityEngine;
using UnityEngine.UI;

namespace BonelabAdvancedHealth
{
    public sealed class ThoughtUI
    {
        private readonly ThoughtPhrase[] _phrases =
        {
            new ThoughtPhrase("lost", "where am i?", ThoughtCondition.Unconscious),
            new ThoughtPhrase("happened", "what happened?", ThoughtCondition.Unconscious),
            new ThoughtPhrase("breathe", "i can't breathe...", ThoughtCondition.LowOxygen),
            new ThoughtPhrase("wake", "wake up...", ThoughtCondition.Unconscious),
            new ThoughtPhrase("pain", "everything hurts...", ThoughtCondition.HighPain),
            new ThoughtPhrase("awake", "stay awake...", ThoughtCondition.Blackout),
            new ThoughtPhrase("help", "i need help...", ThoughtCondition.Unconscious),
            new ThoughtPhrase("ringing", "why is it so loud?", ThoughtCondition.HeadTrauma),
            new ThoughtPhrase("cold", "i feel cold...", ThoughtCondition.LowBlood)
        };

        private GameObject? _root;
        private Text? _text;
        private Font? _font;
        private float _timer;
        private float _alpha;
        private float _visibleSeconds;
        private int _currentIndex = -1;

        public void Update(float deltaTime, HealthManager manager)
        {
            if (!Config.HudEnabled)
            {
                Destroy();
                return;
            }

            Transform? head = MainMod.Runtime?.GetHeadTransform();
            if (head == null)
                return;

            EnsureCreated(head);

            _timer -= deltaTime;
            if (_currentIndex < 0 && _timer <= 0f && ShouldShow(manager))
                PickPhrase(manager);

            if (_currentIndex >= 0)
            {
                _visibleSeconds -= deltaTime;
                float target = _visibleSeconds > 0f ? 1f : 0f;
                _alpha = MoveToward(_alpha, target, deltaTime * 0.9f);
                if (_visibleSeconds <= -1.6f)
                {
                    _currentIndex = -1;
                    _timer = 2.5f + (float)manager.Random.NextDouble() * 5.5f;
                }
            }
            else
            {
                _alpha = MoveToward(_alpha, 0f, deltaTime * 1.2f);
            }

            if (_text != null)
                _text.color = new Color(0.86f, 0.9f, 0.95f, _alpha * 0.92f);
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _text = null;
        }

        private void EnsureCreated(Transform head)
        {
            if (_root != null)
            {
                if (_root.transform.parent != head)
                    Attach(head);
                return;
            }

            _font = Font.GetDefault();
            _root = new GameObject("AHS_ThoughtUI");
            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32001;
            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            scaler.referencePixelsPerUnit = 100f;
            RectTransform rect = _root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600f, 100f);
            Attach(head);

            GameObject textGo = new GameObject("ThoughtText");
            textGo.transform.SetParent(_root.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(600f, 90f);
            textRect.anchoredPosition = Vector2.zero;
            _text = textGo.AddComponent<Text>();
            _text.font = _font;
            _text.fontSize = 25;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.resizeTextForBestFit = true;
            _text.resizeTextMinSize = 14;
            _text.resizeTextMaxSize = 25;
            _text.supportRichText = false;
            _text.raycastTarget = false;
            _text.text = string.Empty;
        }

        private void Attach(Transform head)
        {
            if (_root == null)
                return;

            _root.transform.SetParent(head, false);
            _root.transform.localPosition = new Vector3(0f, -0.38f, 0.76f);
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one * 0.00145f;
        }

        private bool ShouldShow(HealthManager manager)
        {
            return manager.Consciousness.State == ConsciousnessState.Unconscious ||
                   manager.Consciousness.State == ConsciousnessState.Blackout ||
                   manager.Lungs.OxygenNormalized < 0.55f ||
                   manager.PainNormalized > 0.72f ||
                   manager.Brain.HasActiveConcussion;
        }

        private void PickPhrase(HealthManager manager)
        {
            ThoughtCondition condition = GetCondition(manager);
            int firstMatch = -1;
            int matches = 0;
            for (int i = 0; i < _phrases.Length; i++)
            {
                if ((_phrases[i].Condition & condition) == 0)
                    continue;

                matches++;
                if (manager.Random.Next(matches) == 0)
                    firstMatch = i;
            }

            if (firstMatch < 0)
                firstMatch = manager.Random.Next(0, _phrases.Length);

            _currentIndex = firstMatch;
            _visibleSeconds = 1.8f + (float)manager.Random.NextDouble() * 1.8f;
            if (_text != null)
                _text.text = _phrases[_currentIndex].Text;
        }

        private static ThoughtCondition GetCondition(HealthManager manager)
        {
            ThoughtCondition condition = ThoughtCondition.None;
            if (manager.Consciousness.State == ConsciousnessState.Unconscious)
                condition |= ThoughtCondition.Unconscious;
            if (manager.Consciousness.State == ConsciousnessState.Blackout)
                condition |= ThoughtCondition.Blackout;
            if (manager.Lungs.OxygenNormalized < 0.62f)
                condition |= ThoughtCondition.LowOxygen;
            if (manager.Bleeding.BloodNormalized < 0.58f)
                condition |= ThoughtCondition.LowBlood;
            if (manager.PainNormalized > 0.7f)
                condition |= ThoughtCondition.HighPain;
            if (manager.Brain.HasActiveConcussion)
                condition |= ThoughtCondition.HeadTrauma;
            return condition == ThoughtCondition.None ? ThoughtCondition.Unconscious : condition;
        }

        private static float MoveToward(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private readonly struct ThoughtPhrase
        {
            public readonly string Key;
            public readonly string Text;
            public readonly ThoughtCondition Condition;

            public ThoughtPhrase(string key, string text, ThoughtCondition condition)
            {
                Key = key;
                Text = text;
                Condition = condition;
            }
        }

        [Flags]
        private enum ThoughtCondition
        {
            None = 0,
            Unconscious = 1,
            Blackout = 2,
            LowOxygen = 4,
            LowBlood = 8,
            HighPain = 16,
            HeadTrauma = 32
        }
    }
}
