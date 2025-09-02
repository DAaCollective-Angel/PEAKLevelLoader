using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class FogOriginRegistrar
{
    private static FieldInfo _originsField;
    private static Type _orbFogHandlerType;

    static FogOriginRegistrar()
    {
        _orbFogHandlerType = typeof(OrbFogHandler);
        _originsField = _orbFogHandlerType.GetField("origins", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    public static void InsertOriginAtIndex(FogSphereOrigin origin, int idx)
    {
        if (origin == null) return;
        try
        {
            var instance = OrbFogHandler.Instance;
            if (instance == null)
            {
                Debug.LogWarning("FogOriginRegistrar: OrbFogHandler.Instance is null; cannot register fog origin now.");
                return;
            }
            if (_originsField == null)
            {
                Debug.LogWarning("FogOriginRegistrar: couldn't find 'origins' field on OrbFogHandler via reflection.");
                return;
            }

            var current = _originsField.GetValue(instance) as FogSphereOrigin[] ?? Array.Empty<FogSphereOrigin>();
            var list = current.ToList();
            if (idx < 0 || idx > list.Count) idx = list.Count;
            list.Insert(idx, origin);
            var newArr = list.ToArray();
            _originsField.SetValue(instance, newArr);

            var initMethod = _orbFogHandlerType.GetMethod("InitNewSphere", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (initMethod != null)
            {
                var currentIdField = _orbFogHandlerType.GetField("currentID", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                int currentId = (currentIdField != null) ? (int)currentIdField.GetValue(instance) : 0;
                if (currentId >= 0 && currentId < newArr.Length)
                {
                    initMethod.Invoke(instance, new object[] { newArr[currentId] });
                }
            }
            Debug.Log($"FogOriginRegistrar: inserted origin at index {idx}. total origins now = {newArr.Length}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("FogOriginRegistrar failed: " + ex);
        }
    }
}
