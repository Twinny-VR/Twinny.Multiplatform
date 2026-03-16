using Concept.Core;
using System.Collections.Generic;
using Twinny.Mobile.Interactables;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Samples
{
    [RequireComponent(typeof(UIDocument))]
    public class MockupInterface : MonoBehaviour, ITwinnyMobileCallbacks
    {
        private const string CardListName = "FloorCardList";
        private const string EmptyStateLabelName = "EmptyStateLabel";
        private const string CardTemplateName = "MockupCardTemplate";
        private const string CardClassName = "mockup-card";
        private const string CardActiveClassName = "mockup-card--active";
        private const string CardContentClassName = "mockup-card__content";
        private const string CardTitleClassName = "mockup-card__title";
        private const string CardSubtitleClassName = "mockup-card__subtitle";
        private const string CardEnabledClassName = "mockup-card--enabled";

        [SerializeField] private UIDocument _document;
        [SerializeField] private Floor[] _floors = new Floor[0];

        private ScrollView _cardList;
        private Label _emptyStateLabel;
        private VisualElement _cardTemplate;
        private VisualElement _cardsRoot;
        private readonly Dictionary<Floor, VisualElement> _cardsByFloor = new();
        private Floor _selectedFloor;
        private bool _warnedMissingRoot;
        private bool _warnedMissingCardList;
        private bool _isCardListCallbackRegistered;

        private void OnEnable()
        {
            EnsureDocument();
            CacheElements();
            PopulateCards();
            CallbackHub.RegisterCallback<ITwinnyMobileCallbacks>(this);
        }

        private void OnDisable()
        {
            UnregisterCardListCallbacks();
            CallbackHub.UnregisterCallback<ITwinnyMobileCallbacks>(this);
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
        }

        private void CacheElements()
        {
            if (_document == null || _document.rootVisualElement == null)
            {
                WarnMissingRoot();
                return;
            }

            VisualElement root = _document.rootVisualElement;
            _cardList = root.Q<ScrollView>(CardListName);
            _emptyStateLabel = root.Q<Label>(EmptyStateLabelName);
            _cardTemplate = root.Q<VisualElement>(CardTemplateName);

            if (_cardList == null)
            {
                WarnMissingCardList();
                return;
            }

            EnsureCardsRoot();
            RegisterCardListCallbacks();
        }

        private void PopulateCards()
        {
            if (_cardList == null)
                return;

            EnsureCardsRoot();
            if (_cardsRoot == null)
                return;

            RemoveCardTemplateFromRuntimeList();
            _cardsRoot.Clear();
            _cardsByFloor.Clear();

            List<Floor> validFloors = new();
            int floorCount = _floors != null ? _floors.Length : 0;
            for (int i = 0; i < floorCount; i++)
            {
                Floor floor = _floors[i];
                if (floor == null || floor.Data == null || string.IsNullOrWhiteSpace(floor.Data.Title))
                    continue;

                validFloors.Add(floor);
            }

            bool hasFloors = validFloors.Count > 0;
            if (_emptyStateLabel != null)
                _emptyStateLabel.style.display = hasFloors ? DisplayStyle.None : DisplayStyle.Flex;

            if (!hasFloors)
                return;

            for (int i = 0; i < validFloors.Count; i++)
            {
                Floor floor = validFloors[i];
                VisualElement card = CreateFloorCard(floor);
                _cardsRoot.Add(card);
                _cardsByFloor[floor] = card;
            }

            UpdateCardSelection();
        }

        private VisualElement CreateFloorCard(Floor floor)
        {
            FloorData data = floor.Data;
            var card = new VisualElement();
            card.AddToClassList(CardClassName);
            card.AddToClassList(CardEnabledClassName);
            card.AddManipulator(new Clickable(() => HandleFloorCardClicked(floor)));

            var content = new VisualElement();
            content.AddToClassList(CardContentClassName);

            var title = new Label(data.Title.Trim());
            title.AddToClassList(CardTitleClassName);
            content.Add(title);

            string subtitleValue = string.IsNullOrWhiteSpace(data.Subtitle)
                ? "Sem subtitulo"
                : data.Subtitle.Trim();
            var subtitle = new Label(subtitleValue);
            subtitle.AddToClassList(CardSubtitleClassName);
            content.Add(subtitle);

            card.Add(content);
            return card;
        }

        private void HandleFloorCardClicked(Floor floor)
        {
            if (floor == null || floor.Data == null)
                return;

            floor.Select();
        }

        private void SceneRequest(FloorData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.ImmersionSceneName))
                return;

            CallbackHub.CallAction<IMobileUICallbacks>(callback =>
            {
                if (data.SceneOpenMode == FloorData.FloorSceneOpenMode.Mockup)
                    callback.OnMockupRequested(data.ImmersionSceneName);
                else
                    callback.OnImmersiveRequested(data.ImmersionSceneName);
            });
        }

        private void UpdateCardSelection()
        {
            foreach (KeyValuePair<Floor, VisualElement> pair in _cardsByFloor)
            {
                if (pair.Value == null)
                    continue;

                bool isActive = pair.Key == _selectedFloor;
                if (isActive)
                    pair.Value.AddToClassList(CardActiveClassName);
                else
                    pair.Value.RemoveFromClassList(CardActiveClassName);
            }
        }

        public void OnFloorSelected(Floor floor)
        {
            if (floor == null || !_cardsByFloor.ContainsKey(floor))
            {
                _selectedFloor = null;
                UpdateCardSelection();
                return;
            }

            _selectedFloor = floor;
            UpdateCardSelection();
        }

        public void OnFloorUnselected(Floor floor)
        {
            if (_selectedFloor != floor)
                return;

            _selectedFloor = null;
            UpdateCardSelection();
        }

        public void OnFloorFocused(Floor floor) { }
        public void OnStartInteract(GameObject gameObject) { }
        public void OnStopInteract(GameObject gameObject) { }
        public void OnStartTeleport() { }
        public void OnTeleport() { }
        public void OnPlatformInitializing() { }
        public void OnPlatformInitialized() { }
        public void OnExperienceLoaded()
        {
            PopulateCards();
        }
        public void OnExperienceReady() { }
        public void OnExperienceStarting() { }
        public void OnExperienceStarted() { }
        public void OnExperienceEnding() { }
        public void OnExperienceEnded(bool isRunning) { }
        public void OnSceneLoadStart(string sceneName) { }
        public void OnSceneLoaded(Scene scene) { }
        public void OnTeleportToLandMark(int landMarkIndex) { }
        public void OnSkyboxHDRIChanged(Material material) { }
        public void OnEnterImmersiveMode() { }
        public void OnExitImmersiveMode() { }
        public void OnEnterMockupMode() { }
        public void OnExitMockupMode() { }
        public void OnEnterDemoMode() { }
        public void OnExitDemoMode() { }

        private void EnsureCardsRoot()
        {
            if (_cardList == null)
                return;

            if (_cardsRoot != null && _cardsRoot.parent == _cardList.contentContainer)
                return;

            _cardsRoot = _cardList.contentContainer.Q<VisualElement>("MockupCardsRoot");
            if (_cardsRoot != null)
                return;

            _cardsRoot = new VisualElement
            {
                name = "MockupCardsRoot"
            };
            _cardsRoot.style.flexDirection = FlexDirection.Column;
            _cardsRoot.style.flexGrow = 1f;
            _cardsRoot.style.width = Length.Percent(100f);
            _cardList.contentContainer.Add(_cardsRoot);
        }

        private void RemoveCardTemplateFromRuntimeList()
        {
            if (_cardTemplate == null)
                return;

            if (_cardTemplate.parent != null)
                _cardTemplate.RemoveFromHierarchy();

            _cardTemplate = null;
        }

        private void RegisterCardListCallbacks()
        {
            if (_cardList == null || _isCardListCallbackRegistered)
                return;

            _cardList.RegisterCallback<AttachToPanelEvent>(HandleCardListAttached);
            _cardList.RegisterCallback<GeometryChangedEvent>(HandleCardListGeometryChanged);
            _isCardListCallbackRegistered = true;
        }

        private void UnregisterCardListCallbacks()
        {
            if (_cardList == null || !_isCardListCallbackRegistered)
                return;

            _cardList.UnregisterCallback<AttachToPanelEvent>(HandleCardListAttached);
            _cardList.UnregisterCallback<GeometryChangedEvent>(HandleCardListGeometryChanged);
            _isCardListCallbackRegistered = false;
        }

        private void HandleCardListAttached(AttachToPanelEvent evt)
        {
            PopulateCards();
        }

        private void HandleCardListGeometryChanged(GeometryChangedEvent evt)
        {
            if (_cardsRoot == null || _cardsRoot.childCount > 0 || (_floors != null && _floors.Length == 0))
                return;

            PopulateCards();
        }

        private void WarnMissingRoot()
        {
            if (_warnedMissingRoot)
                return;

            _warnedMissingRoot = true;
            Debug.LogWarning("[MockupInterface] UIDocument or rootVisualElement not found.");
        }

        private void WarnMissingCardList()
        {
            if (_warnedMissingCardList)
                return;

            _warnedMissingCardList = true;
            Debug.LogWarning("[MockupInterface] FloorCardList not found in UXML.");
        }
    }
}
