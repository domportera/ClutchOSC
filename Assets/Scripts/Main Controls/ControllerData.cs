using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public abstract class ControllerData
{
    [SerializeField] private string _name;
    [SerializeField] private int _position;
    [SerializeField] private bool _enabled = true;
    [SerializeField] private float _width = NullWidth;
    
    public abstract ControllerType ControlType { get; }
    public abstract string DefaultName { get; }
    public bool IsNamedAsDefault => _name == DefaultName;

    protected const int NullPosition = -1;
    protected const int NullWidth = -1;

    protected ControllerData(string name, int position)
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        _name = name ?? DefaultName;
        _position = position;
    }

    public struct WidthRange
    {
        public readonly float Min;
        public readonly float Max;
        public readonly float DefaultValue;

        public WidthRange(float min, float max, float defaultValue)
        {
            Min = min;
            Max = max;
            DefaultValue = defaultValue;
        }
    }

    public static readonly Dictionary<Type, WidthRange> WidthRanges = new()
    {
        {typeof(Controller2DData), new WidthRange(0.4f, 2f, 1f) },
        {typeof(FaderData), new WidthRange(0.4f, 2f, 1f) }
    };

    public void SetPosition(int index)
    {
        PositionChanged?.Invoke(this, index);
        _position = index;
    }

    public int SortPosition => _position;

    public string Name => _name;

    public void SetName(string name)
    {
        _name = name;
        NameChanged?.Invoke(this, name);
    }

    public void SetWidth(float width)
    {
        _width = width;
        WidthChanged?.Invoke(this, width);
    }

    public float Width => _width <= NullWidth ? WidthRanges[GetType()].DefaultValue : _width;
    public bool Enabled => _enabled;

    public WidthRange GetWidthRange() => WidthRanges[GetType()];

    public void SetEnabled(bool enabled)
    {
        EnabledChanged?.Invoke(this, enabled);
        _enabled = enabled;
    }
    
    public event EventHandler<bool> EnabledChanged;
    public event EventHandler<string> NameChanged;
    public event EventHandler<int> PositionChanged;
    public event EventHandler<float> WidthChanged;
    
    [NonSerialized]
    private EventHandler _onDestroyRequested;
    public EventHandler OnDestroyRequested
    {
        set
        {
            if(value != null && _onDestroyRequested != null)
                throw new InvalidOperationException("DestroyRequested event can only be set once");
            
            _onDestroyRequested = value;
        }
    }

    [NonSerialized]
    public bool DeletionRequested;
    public void InvokeDestroyed() => _onDestroyRequested?.Invoke(this, EventArgs.Empty);
}


[Serializable]
public sealed class FaderData : ControllerData
{
    [SerializeField] private AxisControlSettings _settings;
    
    public override ControllerType ControlType => ControllerType.Fader;
    public override string DefaultName => "New Fader";
    
    public AxisControlSettings Settings => _settings;
    public FaderData(AxisControlSettings config, string name = null, int position = NullPosition) : base(name, position)
    {
        _settings = config ?? throw new ArgumentNullException(nameof(config));
    }
}

[Serializable]
public sealed class Controller2DData : ControllerData
{
    public AxisControlSettings HorizontalAxisControl => _horizontalAxis;
    public AxisControlSettings VerticalAxisControl => _verticalAxis;
    
    public override string DefaultName => "New Controller2D";

    public override ControllerType ControlType => ControllerType.Controller2D;

    [SerializeField] private AxisControlSettings _horizontalAxis;
    [SerializeField] private AxisControlSettings _verticalAxis;
    
    public Controller2DData(AxisControlSettings horizontalConfig, AxisControlSettings verticalConfig, string name = null,
        int position = NullPosition) : base(name, position)
    {
        _horizontalAxis = horizontalConfig ?? throw new ArgumentNullException(nameof(horizontalConfig));
        _verticalAxis = verticalConfig ?? throw new ArgumentNullException(nameof(verticalConfig));
    }
}


