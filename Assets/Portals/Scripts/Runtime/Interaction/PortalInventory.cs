using System.Collections.Generic;
using UnityEngine;

namespace Portals.Interaction
{
    /// <summary>
    /// Minimal inventory with a configurable slot count. Stored objects are deactivated
    /// and reactivated on retrieval, preserving their components and properties.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalInventory : MonoBehaviour
    {
        [Header("Input (no conflicts with portal gun / movement)")]
        [SerializeField] private KeyCode m_StoreKey = KeyCode.Q;
        [SerializeField] private KeyCode m_RetrieveKey = KeyCode.C;
        [SerializeField] private KeyCode m_DropKey = KeyCode.X;
        [SerializeField] private KeyCode m_NextSlotKey = KeyCode.RightBracket;
        [SerializeField] private KeyCode m_PreviousSlotKey = KeyCode.LeftBracket;

        [Header("Inventory")]
        [SerializeField, Min(1)] private int m_Capacity = 4;
        [SerializeField, Min(0.1f)] private float m_RetrieveDistance = 2f;

        [SerializeField] private PortalObjectInteractor m_Interactor;
        [SerializeField] private Camera m_Camera;

        private readonly List<GameObject> m_Slots = new List<GameObject>();

        public int Capacity => m_Capacity;
        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < m_Slots.Count; i++)
                    if (m_Slots[i] != null) count++;
                return count;
            }
        }
        public int SelectedSlot { get; private set; }
        public IReadOnlyList<GameObject> Slots => m_Slots;

        private void Awake()
        {
            for (int i = 0; i < m_Capacity; i++) m_Slots.Add(null);
            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Interactor == null) m_Interactor = GetComponent<PortalObjectInteractor>();
        }

        /// <summary>Wires the inventory to the player's interactor and camera.</summary>
        public void Configure(PortalObjectInteractor interactor, Camera camera)
        {
            m_Interactor = interactor;
            m_Camera = camera;
        }

        private void Update()
        {
            if (Input.GetKeyDown(m_NextSlotKey)) SelectNext();
            if (Input.GetKeyDown(m_PreviousSlotKey)) SelectPrevious();

            // Mouse wheel is the discoverable, conflict-free way to cycle slots.
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (wheel > 0.001f) SelectNext();
            else if (wheel < -0.001f) SelectPrevious();

            if (Input.GetKeyDown(m_StoreKey) && m_Interactor != null)
            {
                GameObject held = m_Interactor.ReleaseHeldForStorage();
                if (held != null)
                {
                    if (!TryAdd(held)) m_Interactor.DropFromInventory(held, held.transform.position);
                }
            }

            if (Input.GetKeyDown(m_RetrieveKey)) RetrieveSelected();

            if (Input.GetKeyDown(m_DropKey))
            {
                Vector3 dropPosition = m_Camera != null
                    ? m_Camera.transform.position + m_Camera.transform.forward * m_RetrieveDistance
                    : transform.position + transform.forward * m_RetrieveDistance;
                Drop(SelectedSlot, dropPosition);
            }
        }

        public bool TryAdd(GameObject obj)
        {
            if (obj == null) return false;
            InteractableObject interactable = obj.GetComponentInParent<InteractableObject>();
            if (interactable != null)
            {
                if (!interactable.FitsInInventory) return false;
            }
            else
            {
                // Dynamic objects without metadata still have a sane size limit.
                Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    Vector3 e = cols[i].bounds.extents;
                    if (Mathf.Max(e.x, Mathf.Max(e.y, e.z)) * 2f > 1.5f + 0.001f)
                        return false;
                }
            }

            int slot = FindEmptySlot();
            if (slot < 0) return false;

            Rigidbody body = obj.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
            }
            obj.SetActive(false);
            obj.transform.SetParent(transform, false);
            m_Slots[slot] = obj;
            return true;
        }

        public GameObject Retrieve(int slot)
        {
            if (slot < 0 || slot >= m_Slots.Count) return null;
            GameObject obj = m_Slots[slot];
            if (obj == null) return null;

            m_Slots[slot] = null;
            obj.transform.SetParent(null, true);
            obj.SetActive(true);
            Camera camera = m_Camera != null ? m_Camera : Camera.main;
            if (camera != null)
                obj.transform.position = camera.transform.position +
                                         camera.transform.forward * m_RetrieveDistance;

            Rigidbody body = obj.GetComponent<Rigidbody>();
            if (body != null) body.useGravity = true;
            return obj;
        }

        public bool Drop(int slot, Vector3 position)
        {
            GameObject obj = Retrieve(slot);
            if (obj == null) return false;
            obj.transform.position = position;
            return true;
        }

        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= m_Capacity) return;
            SelectedSlot = slot;
        }

        public void SelectNext()
        {
            SelectSlot((SelectedSlot + 1) % m_Capacity);
        }

        public void SelectPrevious()
        {
            SelectSlot((SelectedSlot - 1 + m_Capacity) % m_Capacity);
        }

        public GameObject RetrieveSelected() => Retrieve(SelectedSlot);

        private int FindEmptySlot()
        {
            for (int i = 0; i < m_Slots.Count; i++)
                if (m_Slots[i] == null) return i;
            return -1;
        }

        private void OnGUI()
        {
            float height = m_Capacity * 20f + 46f;
            GUILayout.BeginArea(new Rect(16f, Screen.height - height - 8f, 260f, height));
            for (int i = 0; i < m_Capacity; i++)
            {
                string label = m_Slots[i] == null
                    ? $"[{i}] empty"
                    : $"[{i}] {m_Slots[i].name}";
                if (i == SelectedSlot) label = "> " + label;
                GUILayout.Label(label);
            }
            GUILayout.Label("E pick up  Q store  C retrieve  X drop  [ ] / wheel select");
            GUILayout.EndArea();
        }
    }
}
