using DataSaving;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;

public abstract class BaseSaveableListner<T> : MonoBehaviour where T : class, ISaveable, new()
{
    [SerializeField]
    private SaveableEvent OnLoad;
    [SerializeField]
    private SaveableEvent OnValueChanged;

    private T _saveable;
    protected T Saveable => DataHandler.Getter(ref _saveable);

    protected virtual void OnEnable()
    {
        OnLoad?.Invoke(Saveable);
        Saveable.OnValueChange += ValuChanged;
    }
    protected virtual void OnDisable()
    {
        Saveable.OnValueChange -= ValuChanged;
    }
    private void ValuChanged()
    {
        OnValueChanged?.Invoke(Saveable);
    }

    [Serializable]
    public class SaveableEvent : UnityEvent<T> { }
}
