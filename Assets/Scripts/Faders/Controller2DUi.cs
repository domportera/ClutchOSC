using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Faders;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public sealed class Controller2DUi : MonoBehaviour, ISortingMember
{
    [Header("Visuals")] [SerializeField] private RectTransform _rootTransform;
    [SerializeField]
    private RectTransform _horizontalLine;
    [SerializeField] private RectTransform _verticalLine;
    [SerializeField] private RectTransform _centerDot;
    [SerializeField] private Text _valueText;

    private AxisController _horizontalAxisController;
    private AxisController _verticalAxisController;

    [Header("Interaction")]
    [SerializeField]
    private RectTransform _buttonTransform;
    [SerializeField] private EventTrigger _eventTrigger;

    [Header("Aesthetics")]
    [SerializeField]
    private bool _showBorder;
    [SerializeField] private GameObject _border;
    [SerializeField] private Text _title;

    [SerializeField] private List<Image> _controlImages = new();

    [Header("Sorting")]
    [SerializeField]
    private Button _sortLeftButton;
    [SerializeField] private Button _sortRightButton;

    private readonly List<int> _touchIds = new();

    [SerializeField] private float _interactionPadding = 20f;
    
    private ControllerData _controllerData;
    private Vector2 _initialSizeDelta;


    private void Awake()
    {
        if(!_rootTransform)
            _rootTransform = GetComponent<RectTransform>();
        
        _border.SetActive(_showBorder);
    }

    // Update is called once per frame
    private void Update()
    {
        SetTargetPosition();
        var dt = Time.unscaledDeltaTime;
        _verticalAxisController.Update(dt);
        _horizontalAxisController.Update(dt);
        MoveSliders();
        const string fmt = "({0}, {1})";
        var culture = CultureInfo.CurrentCulture;
        _valueText.text = string.Format(fmt, _horizontalAxisController.LatestSentValue, _verticalAxisController.LatestSentValue);
    }

    public void Initialize(Controller2DData data)
    {
        InitializeButtonInteraction();
        _verticalAxisController = new AxisController(data.VerticalAxisControl);
        _horizontalAxisController = new AxisController(data.HorizontalAxisControl);
        _initialSizeDelta = GetComponent<RectTransform>().sizeDelta;
        InitializeSorting();
        OnEnabledChanged(this, data.Enabled);
        OnNameChanged(this, data.Name);
        data.EnabledChanged += OnEnabledChanged;
        data.WidthChanged += OnWidthChanged;
        data.NameChanged += OnNameChanged;
        _controllerData = data;
    }

    private void OnNameChanged(object sender, string text)
    {
        _title.text = text;
    }

    private void OnWidthChanged(object sender, ControllerData.WidthArgs args)
    {
        var rectTransform = GetComponent<RectTransform>();
        Width.UpdateWidth(rectTransform, args, _initialSizeDelta);
    }

    private void OnEnabledChanged(object sender, bool e)
    {
        _rootTransform.gameObject.SetActive(e);
    }

    private void OnDestroy()
    {
        _controllerData.EnabledChanged -= OnEnabledChanged;
        _controllerData.WidthChanged -= OnWidthChanged;
    }

    private void InitializeButtonInteraction()
    {
        var startEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };
        
        startEntry.callback.AddListener(StartTouch);
        _eventTrigger.triggers.Add(startEntry);
        
        var endEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };

        endEntry.callback.AddListener(EndTouch);
        _eventTrigger.triggers.Add(endEntry);
        return;

        void StartTouch(BaseEventData arg0)
        {
            // pointer down
            var pointerEventData = (PointerEventData)arg0;
            _touchIds.Add(pointerEventData.pointerId);
        }
        
        void EndTouch(BaseEventData arg0)
        {
            // pointer up
            var pointerEventData = (PointerEventData)arg0;
            _touchIds.Remove(pointerEventData.pointerId);
        }
    }


    private void MoveSliders()
    {
        //this inverse lerp is technically unnecessary at time of writing, but if for whatever reason the min and max controller value changes from 0-1, this will handle that
        var xPercent = Mathf.InverseLerp(AxisController.MinControllerValue, AxisController.MaxControllerValue, _horizontalAxisController.SmoothValue);
        var yPercent = Mathf.InverseLerp(AxisController.MinControllerValue, AxisController.MaxControllerValue, _verticalAxisController.SmoothValue);

        var interactionPadding = _interactionPadding;
        
        _buttonTransform.GetWorldCorners(_cornerArray);
        
        var min = Vector3.one * float.MaxValue;
        var max = Vector3.one * float.MinValue;
        
        for (int i = 0; i < _cornerArray.Length; i++)
        {
            min = Vector3.Min(min, _cornerArray[i]);
            max = Vector3.Max(max, _cornerArray[i]);
        }
        
        min.x += interactionPadding;
        max.x -= interactionPadding;
        min.y += interactionPadding;
        max.y -= interactionPadding;
        
        var verticalPosition = _verticalLine.position;
        verticalPosition = new Vector3(Mathf.Lerp(min.x, max.x, xPercent),
            verticalPosition.y,
            verticalPosition.z);
        
        var horizontalPosition = _horizontalLine.position;
        horizontalPosition = new Vector3(horizontalPosition.x,
            Mathf.Lerp(min.y, max.y, yPercent),
            horizontalPosition.z);
        
        _verticalLine.position = verticalPosition;
        _horizontalLine.position = horizontalPosition;
        _centerDot.position = new Vector3(verticalPosition.x, horizontalPosition.y, _centerDot.position.z);
    }

    private void SetTargetPosition()
    {
        if (!TryGetExistingTouch(out var touchPos))
        {
            _horizontalAxisController.Release();
            _verticalAxisController.Release();
            return;
        }

        var touchPositionAsPercentage = GetNormalizedPositionWithinButton(touchPos);

        Vector2 mappedTouchPosition = new()
        {
        //this mapping is technically unnecessary at time of writing, but if for whatever reason the min and max controller value changes from 0-1, this will handle that
            x = touchPositionAsPercentage.x.Map(0f, 1f, AxisController.MinControllerValue, AxisController.MaxControllerValue),
            y = touchPositionAsPercentage.y.Map(0f, 1f, AxisController.MinControllerValue, AxisController.MaxControllerValue)
        };

        _horizontalAxisController.SetValue(mappedTouchPosition.x);
        _verticalAxisController.SetValue(mappedTouchPosition.y);
    }

    private bool TryGetExistingTouch(out Vector2 o)
    {
        var touches = Input.touches;
        for (int i = 0; i < touches.Length; i++)
        {
            for (int j = 0; j < _touchIds.Count; j++)
            {
                if (touches[i].fingerId != _touchIds[j]) continue;
                o = touches[i].position;
                return true;

            }
        }
        
        //https://docs.unity3d.com/2019.1/Documentation/ScriptReference/EventSystems.PointerEventData-pointerId.html
        o = Input.mousePosition;
        return Input.GetMouseButton(0) && _touchIds.Any(x => x== -1); // left click
    }

    /// <summary>
    /// Returns touch position on button as a percentage of its x and y dimensions
    /// </summary>
    /// <returns></returns>
    private Vector2 GetNormalizedPositionWithinButton(Vector2 touchPos)
    {
        var rectTransform = _buttonTransform;

        rectTransform.GetWorldCorners(_cornerArray);

        var min = Vector3.one * float.MaxValue;
        var max = Vector3.one * float.MinValue;

        for (int i = 0; i < _cornerArray.Length; i++)
        {
            min = Vector3.Min(min, _cornerArray[i]);
            max = Vector3.Max(max, _cornerArray[i]);
        }
        
        min.x += _interactionPadding;
        max.x -= _interactionPadding;
        min.y += _interactionPadding;
        max.y -= _interactionPadding;
        
        var minScreen = RectTransformUtility.WorldToScreenPoint(null, min);
        var maxScreen = RectTransformUtility.WorldToScreenPoint(null, max);
        var xPercent = Mathf.InverseLerp(minScreen.x, maxScreen.x, touchPos.x);
        var yPercent = Mathf.InverseLerp(minScreen.y, maxScreen.y, touchPos.y);

        return new Vector2(xPercent, yPercent);
    }

    #region Sorting
    private void InitializeSorting()
    {
        _sortLeftButton.onClick.AddListener(() =>
        {
            transform.SetSiblingIndex(transform.GetSiblingIndex() - 1);
        });
        _sortRightButton.onClick.AddListener(() =>
        {
            transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        });
    }

    public void SetSortButtonVisibility(bool visible)
    {
        _sortLeftButton.gameObject.SetActive(visible);
        _sortRightButton.gameObject.SetActive(visible);
        foreach (var i in _controlImages)
        {
            i.enabled = !visible;
        }
    }

    public RectTransform RectTransform => _rootTransform;
    private readonly Vector3[] _cornerArray = new Vector3[4]; 

    #endregion Sorting

}
