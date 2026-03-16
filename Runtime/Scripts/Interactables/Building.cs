using System;
using UnityEngine;

namespace Twinny.Mobile.Interactables
{
    [Serializable]
    public class BuildingFloorEntry
    {
        [SerializeField] private GameObject _root;

        public GameObject Root => _root;
        public Transform RootTransform => _root != null ? _root.transform : null;
        public Floor FloorComponent => _root != null ? _root.GetComponent<Floor>() : null;
        public bool HasInteractiveFloor => FloorComponent != null;
    }

    public class Building : MonoBehaviour
    {
        [SerializeField] private BuildingFloorEntry[] _floors;

        public BuildingFloorEntry[] Floors => _floors;
    }
}
