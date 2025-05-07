using System;
using UnityEngine;

namespace RealtimeCSG
{
    internal abstract class BaseEditMode : ScriptableObject
    {
        public abstract void OnForceRenderUpdate();
    }
}