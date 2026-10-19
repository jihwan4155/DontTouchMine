using System;
using System.Linq;
using Game.Client.Interactions;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Client.Items
{
    /// <summary>Offline authoring playground. Changes affect instances, never catalog assets.</summary>
    public sealed class ItemSoloLab : MonoBehaviour
    {
        public ItemCatalogSO catalog;
        public Camera viewCamera;
        public Font reviewFont;
        public InteractionConfigSO interactionConfig;
        public Transform testWall;
        public CarryableItem Current { get; private set; }
        public string Warning { get; private set; } = "";
        public string LastContact { get; private set; } = "아직 충돌 없음";
        public float PreviewScale { get; set; } = 1;
        public float MinimumThickness { get; set; }
        public bool UseDiscrete { get; set; }
        public float ThrowSpeed { get; set; } = 10;
        public float ThrowAngle { get; set; } = -10;
        public int CategoryCount => categories.Length;
        public int ItemCount => categories.Length == 0 ? 0 : categories[categoryIndex].items.Count;

        private ItemCatalogSO.Category[] categories = Array.Empty<ItemCatalogSO.Category>();
        private int categoryIndex, itemIndex;
        private bool showReserve, showColliders = true;
        private Vector2 itemScroll, detailScroll;
        private Vector3 originalDimensions, colliderDimensions;
        private Transform hand;
        private Material wireMaterial;
        private LineRenderer[] wires = Array.Empty<LineRenderer>();
        private Collider[] visibleColliders = Array.Empty<Collider>();
        private string status = "저장된 프리팹 · 원본 배율";
        private float yaw, pitch;

        private void Start()
        {
            if (catalog == null) catalog = ItemCatalogSO.Load(false);
            if (viewCamera == null) viewCamera = Camera.main;
            hand = new GameObject("Lab hold point").transform;
            hand.SetParent(transform, false);
            hand.position = new Vector3(0, 1.8f, -2);
            wireMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            wireMaterial.SetColor("_BaseColor", new Color(1, .82f, .15f));
            if (interactionConfig != null) ThrowSpeed = interactionConfig.ThrowSpeed;
            RefreshCategories();
            HomeCamera();
        }

        private void RefreshCategories()
        {
            categories = catalog.categories.Where(c => (c.enabled || showReserve) && c.items.Any(i => i.prefab != null)).ToArray();
            categoryIndex = Mathf.Clamp(categoryIndex, 0, categories.Length - 1);
            itemIndex = 0;
            SpawnSelection();
        }

        public bool SelectItem(string categoryId, string itemId)
        {
            var category = Array.FindIndex(categories, c => c.id == categoryId);
            if (category < 0) return false;
            var item = categories[category].items.FindIndex(i => i.id == itemId && i.prefab != null);
            if (item < 0) return false;
            categoryIndex = category;
            itemIndex = item;
            PreviewScale = 1;
            MinimumThickness = 0;
            UseDiscrete = false;
            SpawnSelection();
            return true;
        }

        public void SpawnSelection()
        {
            if (categories.Length == 0) return;
            if (Current != null) { Current.gameObject.SetActive(false); Destroy(Current.gameObject); }
            var entry = categories[categoryIndex].items[itemIndex];
            if (entry.prefab == null) return;
            var instance = Instantiate(entry.prefab, Vector3.zero, Quaternion.identity);
            instance.name = entry.displayName + " [LAB]";
            Current = instance.GetComponent<CarryableItem>();
            if (Current == null) { Destroy(instance); Warning = "CarryableItem이 없는 프리팹입니다."; return; }
            originalDimensions = MeshBounds(instance).size;
            instance.transform.localScale *= Mathf.Clamp(PreviewScale, .1f, 3);
            foreach (var box in instance.GetComponentsInChildren<BoxCollider>(true))
            {
                var scale = box.transform.lossyScale;
                box.size = Vector3.Max(box.size, new Vector3(
                    MinimumThickness / Mathf.Max(Mathf.Abs(scale.x), .0001f),
                    MinimumThickness / Mathf.Max(Mathf.Abs(scale.y), .0001f),
                    MinimumThickness / Mathf.Max(Mathf.Abs(scale.z), .0001f)));
            }
            var bounds = MeshBounds(instance);
            Current.OnPlaced(new Vector3(0, .02f - bounds.min.y, 0), Quaternion.identity);
            var body = instance.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.collisionDetectionMode = UseDiscrete ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;
            instance.AddComponent<ItemSoloLabContact>().lab = this;
            visibleColliders = instance.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
            Physics.SyncTransforms();
            var combined = visibleColliders[0].bounds;
            foreach (var collider in visibleColliders.Skip(1)) combined.Encapsulate(collider.bounds);
            colliderDimensions = combined.size;
            CreateWires(instance.transform);
            Warning = "";
            LastContact = "아직 충돌 없음";
            status = "전시 중 · 물리 정지";
        }

        private static Bounds MeshBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>().Where(r => !(r is LineRenderer)).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        public void Drop()
        {
            if (Current == null) return;
            Current.OnReleased(new Pose(new Vector3(0, 2, 0), Quaternion.identity), Vector3.zero);
            status = "2 m 높이에서 낙하";
        }

        public void Throw()
        {
            if (Current == null) return;
            var angle = ThrowAngle * Mathf.Deg2Rad;
            var velocity = new Vector3(0, Mathf.Sin(angle), Mathf.Cos(angle)) * ThrowSpeed;
            // The same release method used by authoritative gameplay; no network runner is needed.
            Current.OnReleased(new Pose(new Vector3(0, 1.8f, -2), Quaternion.identity), velocity);
            status = $"던지는 중 · {ThrowSpeed:F1} m/s · {ThrowAngle:F0}°";
        }

        public void Hold()
        {
            if (Current == null) return;
            Current.OnPickedUp(hand);
            status = "들기 상태 · 실제 CarryableItem 사용";
        }

        public void RecordContact(string name, float speed) => LastContact = $"{name} · 충돌 속도 {speed:F2} m/s";

        private void Update()
        {
            if (Current != null)
            {
                var p = Current.transform.position;
                if (p.y < -1) Warning = "바닥 아래로 이탈했습니다. 복구 후 두께/속도를 비교하세요.";
                else if (Mathf.Abs(p.x) > 10 || p.z < -10 || p.z > 10) Warning = "테스트 바닥 범위를 벗어났습니다. 바닥 관통과 구분하세요.";
                else if (!Current.gameObject.activeInHierarchy) Warning = "아이템이 비활성화되었습니다.";
            }
            if (viewCamera != null)
            {
                var left = Mathf.Min(312, Screen.width * .28f);
                var right = Mathf.Min(332, Screen.width * .29f);
                viewCamera.rect = new Rect(left / Screen.width, 0, 1 - (left + right) / Screen.width, 1);
            }
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.rightArrowKey.wasPressedThisFrame) Next(1);
            if (keyboard.leftArrowKey.wasPressedThisFrame) Next(-1);
            if (keyboard.spaceKey.wasPressedThisFrame) Throw();
            if (keyboard.gKey.wasPressedThisFrame) Drop();
            if (keyboard.hKey.wasPressedThisFrame) Hold();
            if (keyboard.rKey.wasPressedThisFrame) SpawnSelection();
            if (keyboard.cKey.wasPressedThisFrame) showColliders = !showColliders;
            if (keyboard.homeKey.wasPressedThisFrame) HomeCamera();
            if (keyboard.fKey.wasPressedThisFrame && Current != null)
            {
                var bounds = MeshBounds(Current.gameObject);
                viewCamera.transform.position = bounds.center + new Vector3(1, .7f, -1.5f).normalized * Mathf.Max(.5f, bounds.size.magnitude * 1.8f);
                viewCamera.transform.LookAt(bounds.center);
                ReadCameraAngles();
            }
            if (!mouse.rightButton.isPressed || viewCamera == null) return;
            var delta = mouse.delta.ReadValue();
            yaw += delta.x * .15f;
            pitch = Mathf.Clamp(pitch - delta.y * .15f, -85, 85);
            viewCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            var move = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            viewCamera.transform.Translate(move * Time.unscaledDeltaTime * (keyboard.leftShiftKey.isPressed ? 8 : 3));
        }

        private void Next(int step)
        {
            if (ItemCount == 0) return;
            itemIndex = (itemIndex + step + ItemCount) % ItemCount;
            PreviewScale = 1; MinimumThickness = 0; UseDiscrete = false;
            SpawnSelection();
        }

        private void HomeCamera()
        {
            if (viewCamera == null) return;
            viewCamera.transform.position = new Vector3(5, 3.2f, -7);
            viewCamera.transform.LookAt(new Vector3(-.3f, 1, 1));
            ReadCameraAngles();
        }
        private void ReadCameraAngles() { yaw = viewCamera.transform.eulerAngles.y; pitch = viewCamera.transform.eulerAngles.x; if (pitch > 180) pitch -= 360; }

        private void CreateWires(Transform parent)
        {
            wires = new LineRenderer[visibleColliders.Length * 12];
            for (var i = 0; i < wires.Length; i++)
            {
                var go = new GameObject("Collider outline");
                go.transform.SetParent(parent, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = wireMaterial;
                line.positionCount = 2;
                line.startWidth = line.endWidth = .003f;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                wires[i] = line;
            }
        }

        private void LateUpdate()
        {
            var index = 0;
            foreach (var collider in visibleColliders)
            {
                if (collider == null) continue;
                for (var corner = 0; corner < 8; corner++)
                for (var axis = 0; axis < 3; axis++)
                {
                    if ((corner & (1 << axis)) != 0) continue;
                    var wire = wires[index++];
                    if (wire == null) continue;
                    wire.enabled = showColliders && Current != null;
                    Vector3 Point(int bits)
                    {
                        var signs = new Vector3((bits & 1) == 0 ? -1 : 1, (bits & 2) == 0 ? -1 : 1, (bits & 4) == 0 ? -1 : 1);
                        if (collider is BoxCollider box) return box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, signs));
                        return collider.bounds.center + Vector3.Scale(collider.bounds.extents, signs);
                    }
                    wire.SetPosition(0, Point(corner));
                    wire.SetPosition(1, Point(corner | (1 << axis)));
                }
            }
        }

        private static string Dimensions(Vector3 size) => $"{size.x:F3} × {size.y:F3} × {size.z:F3} m";

        private void OnGUI()
        {
            if (categories.Length == 0) return;
            var oldFont = GUI.skin.font;
            if (reviewFont != null) GUI.skin.font = reviewFont;
            var label = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14 };
            var left = Mathf.Min(300, Screen.width * .28f - 12);
            var right = Mathf.Min(320, Screen.width * .29f - 12);
            GUILayout.BeginArea(new Rect(8, 8, left, Screen.height - 16), GUI.skin.box);
            GUILayout.Label("아이템 혼자 테스트", new GUIStyle(label) { fontSize = 21 });
            GUILayout.Label("카테고리 → 아이템을 선택하세요", label);
            var reserve = GUILayout.Toggle(showReserve, "비활성 / 보류 카테고리도 표시");
            if (reserve != showReserve) { showReserve = reserve; RefreshCategories(); }
            var category = GUILayout.SelectionGrid(categoryIndex, categories.Select(c => c.label).ToArray(), 2);
            if (category != categoryIndex) { categoryIndex = category; itemIndex = 0; PreviewScale = 1; MinimumThickness = 0; UseDiscrete = false; itemScroll = Vector2.zero; SpawnSelection(); }
            GUILayout.Label($"{categories[categoryIndex].label} · {ItemCount}개", label);
            itemScroll = GUILayout.BeginScrollView(itemScroll);
            for (var i = 0; i < ItemCount; i++)
                if (GUILayout.Button((i == itemIndex ? "▶ " : "") + categories[categoryIndex].items[i].displayName, GUILayout.Height(27)))
                { itemIndex = i; PreviewScale = 1; MinimumThickness = 0; UseDiscrete = false; SpawnSelection(); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(Screen.width - right - 8, 8, right, Screen.height - 16), GUI.skin.box);
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            GUILayout.Label(categories[categoryIndex].items[itemIndex].displayName, new GUIStyle(label) { fontSize = 19 });
            GUILayout.Label($"{itemIndex + 1} / {ItemCount} · {categories[categoryIndex].items[itemIndex].id}", label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("← 이전")) Next(-1);
            if (GUILayout.Button("다음 →")) Next(1);
            GUILayout.EndHorizontal();
            GUILayout.Label("저장된 메시: " + Dimensions(originalDimensions), label);
            GUILayout.Label("현재 메시: " + Dimensions(originalDimensions * PreviewScale), label);
            GUILayout.Label("충돌체 전체: " + Dimensions(colliderDimensions), label);
            if (Mathf.Min(colliderDimensions.x, colliderDimensions.y, colliderDimensions.z) < .03f)
                GUILayout.Label("얇은 충돌체: 3 cm 미만 축이 있습니다.", label);
            showColliders = GUILayout.Toggle(showColliders, "충돌체 윤곽 표시 (C)");
            GUILayout.Label("변경값은 이 테스트 인스턴스에만 적용", label);
            GUILayout.Label($"크기 배율: {PreviewScale:F2}×", label);
            var scale = GUILayout.HorizontalSlider(PreviewScale, .1f, 3);
            GUILayout.Label($"최소 BoxCollider 두께: {MinimumThickness * 100:F1} cm (0 = 원본)", label);
            var thickness = GUILayout.HorizontalSlider(MinimumThickness, 0, .15f);
            var discrete = GUILayout.Toggle(UseDiscrete, "Discrete 충돌 비교 (기본은 연속 감지)");
            if (Mathf.Abs(scale - PreviewScale) > .001f || Mathf.Abs(thickness - MinimumThickness) > .0001f || discrete != UseDiscrete)
            { PreviewScale = scale; MinimumThickness = thickness; UseDiscrete = discrete; SpawnSelection(); }
            if (GUILayout.Button("원본 크기·충돌체로 되돌리기")) { PreviewScale = 1; MinimumThickness = 0; UseDiscrete = false; SpawnSelection(); }
            GUILayout.Label($"던지기 속도: {ThrowSpeed:F1} m/s", label);
            ThrowSpeed = GUILayout.HorizontalSlider(ThrowSpeed, 1, 30);
            GUILayout.Label($"던지기 각도: {ThrowAngle:F0}°", label);
            ThrowAngle = GUILayout.HorizontalSlider(ThrowAngle, -60, 45);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("들기 H")) Hold();
            if (GUILayout.Button("낙하 G")) Drop();
            if (GUILayout.Button("던지기 Space")) Throw();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("아이템 복구 / 재배치 (R)")) SpawnSelection();
            if (testWall != null)
            {
                testWall.gameObject.SetActive(GUILayout.Toggle(testWall.gameObject.activeSelf, "얇은 벽 충돌 테스트"));
                GUILayout.Label($"벽 두께: {testWall.localScale.z * 100:F1} cm", label);
                var wallSize = testWall.localScale;
                wallSize.z = GUILayout.HorizontalSlider(wallSize.z, .01f, .5f);
                testWall.localScale = wallSize;
            }
            GUILayout.Label(status, label);
            GUILayout.Label("마지막 충돌: " + LastContact, label);
            if (Current != null) GUILayout.Label($"높이 {Current.transform.position.y:F2} m · 속도 {Current.GetComponent<Rigidbody>().linearVelocity.magnitude:F2} m/s", label);
            if (!string.IsNullOrEmpty(Warning)) { var old = GUI.color; GUI.color = new Color(1, .5f, .35f); GUILayout.Label(Warning, label); GUI.color = old; }
            GUILayout.Label($@"물리 간격: {Time.fixedDeltaTime:F3}s
노란 선: BoxCollider (그 외는 바운드)
곰: 실제 캐릭터 1배 크기
우클릭+WASD 이동 / Q·E 높이
F 물건 확대 / Home 전체 보기", label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.skin.font = oldFont;
        }

        private void OnDestroy()
        {
            if (Current != null) Destroy(Current.gameObject);
            if (wireMaterial != null) Destroy(wireMaterial);
        }
    }
}
