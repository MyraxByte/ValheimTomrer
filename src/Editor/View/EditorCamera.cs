using UnityEngine;

namespace ValheimTomrer.Editor.View
{
    /// <summary>The looks the view can snap to: the six sides of the blueprint and a corner.</summary>
    internal enum ViewPreset
    {
        Front,
        Back,
        Right,
        Left,
        Top,
        Bottom,
        Iso,
    }

    /// <summary>Where the camera was: a saved view.</summary>
    internal struct CameraPose
    {
        public Vector3 Position;
        public float Yaw;
        public float Pitch;
        public float Distance;
        public bool Orthographic;
    }

    /// <summary>
    /// Where the pane looks from. One camera: it always flies free, like a player in fly mode.
    /// Which region has the mouse is not a camera setting, it is <see cref="Ui.ViewportHost.Captured"/>.
    ///
    /// Everything here is in the editor scene's own space: the camera hangs under the scene root,
    /// so y = 0 is the grid and the ground, 8000 m under the player's world.
    /// </summary>
    internal sealed class EditorCamera
    {
        private const float PitchMin = -89f;          // never quite straight up or down
        private const float PitchMax = 89f;
        private const float MinEye = 0.1f;            // keys and sticks stop this high above the ground
        private const float FlySpeed = 5f;            // m/s, Shift x3
        private const float FlyBoost = 3f;
        private const float PadFlySpeed = 6f;         // m/s, L1 x3
        private const float PadTurn = 110f;           // degrees a second, the game's (PlayerController.LateUpdate)
        private const float LookStep = 0.05f;         // degrees per mouse pixel, the game's (ZInput's mouse delta scale)
        private const float LookJump = 300f;          // pixels in one move: more is a jump, not a hand
        private const float NearestDistance = 0.3f;      // zoom stops here ...
        private const float FarthestDistance = 1500f;    // ... and here, inside the 2000 m far plane
        private const float DragSpeed = 0.6f;         // a drag turns slower than the same pixels of mouse look

        private readonly PreviewCamera _camera;

        private Vector3 _pos = new Vector3(8f, 6f, 12f);
        private float _yaw;      // 0 looks along +Z, turning right makes it bigger
        private float _pitch;    // up is positive
        private float _dist = 10f;
        private float _near = 0.05f;
        private bool _ortho;

        public EditorCamera(PreviewCamera camera)
        {
            _camera = camera;
            LookFrom(_pos, Vector3.zero);
        }

        /// <summary>Where the camera is, in the editor scene's space.</summary>
        public Vector3 Position => _pos;

        public float Pitch => _pitch;

        public float Yaw => _yaw;

        public float FieldOfView => _camera.Unity.fieldOfView;

        /// <summary>No perspective: parallel lines stay parallel, so a side view can be measured by eye.</summary>
        public bool Orthographic => _ortho;

        /// <summary>How far ahead the point the camera turns, pans and zooms around sits.</summary>
        public float Distance => _dist;

        public Vector3 Forward => Quaternion.Euler(-_pitch, _yaw, 0f) * Vector3.forward;

        /// <summary>Right on the screen, level with the ground.</summary>
        public Vector3 Right => new Vector3(Mathf.Cos(_yaw * Mathf.Deg2Rad), 0f, -Mathf.Sin(_yaw * Mathf.Deg2Rad));

        public Vector3 Up => Vector3.Cross(Forward, Right);

        /// <summary>The point the camera looks at, <see cref="Distance"/> straight ahead.</summary>
        public Vector3 Pivot => _pos + Forward * _dist;

        /// <summary>Puts the camera at <paramref name="from"/>, looking at <paramref name="at"/>, which becomes the point it turns around.</summary>
        public void LookFrom(Vector3 from, Vector3 at)
        {
            _pos = from;
            var direction = at - from;
            _dist = Mathf.Max(direction.magnitude, 0.01f);
            direction /= _dist;
            _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            Apply();
        }

        /// <summary>Looks at the box from its front (+Z side), a bit above and to one side.</summary>
        public void Frame(Bounds box)
        {
            var center = box.center;
            var size = Mathf.Max(Mathf.Max(box.size.x, box.size.y), Mathf.Max(box.size.z, 1f));
            var distance = size * 1.25f + 2.5f;
            var direction = new Vector3(0.28f, 0.5f, 1f).normalized;
            _near = distance / 500f;
            _camera.SetNearPlane(_near);
            LookFrom(center + direction * distance, center);
        }

        /// <summary>
        /// Looks from where another camera looked. A world change kills the old camera's objects,
        /// not its numbers, so the rebuilt pane comes back on the same view.
        /// </summary>
        public void TakePose(EditorCamera from)
        {
            _pos = from._pos;
            _yaw = from._yaw;
            _pitch = from._pitch;
            _dist = from._dist;
            _near = from._near;
            _ortho = from._ortho;
            _camera.SetNearPlane(_near);
            Apply();
        }

        /// <summary>Moves the camera (and the point in front with it). Keys and sticks stop just above the ground.</summary>
        public void Move(Vector3 by)
        {
            if (by.sqrMagnitude == 0f)
            {
                return;
            }

            var before = _pos.y;
            _pos += by;
            if (by.y < 0f)
            {
                _pos.y = Mathf.Max(_pos.y, Mathf.Min(before, MinEye));
            }

            Apply();
        }

        /// <summary>Everything that says where the camera is and how it looks, for a saved view.</summary>
        private static readonly Vector3 IsoLook = -new Vector3(0.28f, 0.5f, 1f).normalized;
        private static readonly float IsoYaw = Mathf.Atan2(IsoLook.x, IsoLook.z) * Mathf.Rad2Deg;
        private static readonly float IsoPitch = Mathf.Asin(IsoLook.y) * Mathf.Rad2Deg;

        public CameraPose Pose => new CameraPose { Position = _pos, Yaw = _yaw, Pitch = _pitch, Distance = _dist, Orthographic = _ortho };

        /// <summary>Goes back to a saved view.</summary>
        public void SetPose(CameraPose pose)
        {
            _pos = pose.Position;
            _yaw = pose.Yaw;
            _pitch = pose.Pitch;
            _dist = Mathf.Max(0.05f, pose.Distance);
            _ortho = pose.Orthographic;
            Apply();
        }

        /// <summary>Perspective or orthographic, keeping the pivot and how much of the scene is seen.</summary>
        public void SetOrthographic(bool on)
        {
            _ortho = on;
            Apply();
        }

        /// <summary>
        /// Looks at the pivot from one side of the blueprint, keeping the distance and the pivot, like the
        /// number keys of a 3D tool do. Top and Bottom keep the way the view is turned, rounded to a
        /// quarter, so the grid stays square on the screen.
        /// </summary>
        public void SetView(ViewPreset view)
        {
            var pivot = Pivot;
            switch (view)
            {
                case ViewPreset.Front:
                    _yaw = 180f;
                    _pitch = 0f;
                    break;
                case ViewPreset.Back:
                    _yaw = 0f;
                    _pitch = 0f;
                    break;
                case ViewPreset.Right:
                    _yaw = -90f;
                    _pitch = 0f;
                    break;
                case ViewPreset.Left:
                    _yaw = 90f;
                    _pitch = 0f;
                    break;
                case ViewPreset.Top:
                    _yaw = Mathf.Round(_yaw / 90f) * 90f;
                    _pitch = -90f;
                    break;
                case ViewPreset.Bottom:
                    _yaw = Mathf.Round(_yaw / 90f) * 90f;
                    _pitch = 90f;
                    break;
                default:
                    // The corner the Frame key looks from.
                    _yaw = IsoYaw;
                    _pitch = IsoPitch;
                    break;
            }

            _pos = pivot - (Forward * _dist);
            Apply();
        }

        /// <summary>
        /// The side the view looks from now, or null when it is not square on to one. The gizmo and the
        /// status show it.
        /// </summary>
        public ViewPreset? CurrentView
        {
            get
            {
                const float Tolerance = 0.6f;
                if (_pitch <= -90f + Tolerance)
                {
                    return ViewPreset.Top;
                }

                if (_pitch >= 90f - Tolerance)
                {
                    return ViewPreset.Bottom;
                }

                if (Mathf.Abs(Mathf.DeltaAngle(_yaw, IsoYaw)) < Tolerance && Mathf.Abs(_pitch - IsoPitch) < Tolerance)
                {
                    return ViewPreset.Iso;
                }

                if (Mathf.Abs(_pitch) > Tolerance)
                {
                    return null;
                }

                var yaw = Mathf.Repeat(_yaw, 360f);
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, 180f)) < Tolerance)
                {
                    return ViewPreset.Front;
                }

                if (Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) < Tolerance)
                {
                    return ViewPreset.Back;
                }

                if (Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < Tolerance)
                {
                    return ViewPreset.Right;
                }

                return Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < Tolerance ? ViewPreset.Left : (ViewPreset?)null;
            }
        }

        /// <summary>
        /// Turns the camera round a point (the selection's middle, or the pivot), keeping that point
        /// where it shows on the screen. Right and up are in degrees, like <see cref="Turn"/>: dragging
        /// right turns the scene to the right.
        /// </summary>
        public void Orbit(Vector3 point, float right, float up)
        {
            var nextPitch = Limit(_pitch, up, PitchMin, PitchMax);
            var yawRot = Quaternion.AngleAxis(right, Vector3.up);
            var pitchRot = Quaternion.AngleAxis(-(nextPitch - _pitch), yawRot * Right);
            _pos = point + (pitchRot * (yawRot * (_pos - point)));
            _yaw += right;
            _pitch = nextPitch;
            if (!_ortho)
            {
                // In perspective the point is what the camera turns round and zooms toward. Without it
                // the distance is only the size of the box seen, which must not jump.
                // A point beside or behind the camera has no depth ahead: keep the old distance then.
                var ahead = Vector3.Dot(point - _pos, Forward);
                if (ahead > NearestDistance)
                {
                    _dist = Mathf.Min(ahead, FarthestDistance);
                }
            }

            Apply();
        }

        /// <summary>A mouse drag with Alt held orbits: a drag over the pane's whole height is a full circle.</summary>
        public void OrbitDrag(Vector3 point, Vector2 pixels, float viewHeight)
        {
            var k = 360f / Mathf.Max(1f, viewHeight);
            Orbit(point, pixels.x * k, pixels.y * k);
        }

        /// <summary>The pad's right stick with L1 held: the same turn speed as looking, but round a point.</summary>
        public void OrbitPad(Vector3 point, Vector2 stick, float dt)
        {
            if (stick == Vector2.zero)
            {
                return;
            }

            var step = PadTurn * PlayerController.m_gamepadSens * dt;
            Orbit(point,
                stick.x * step * (PlayerController.m_invertCameraX ? -1f : 1f),
                stick.y * step * (PlayerController.m_invertCameraY ? -1f : 1f));
        }

        /// <summary>Turns in place. Right and up are positive, in degrees.</summary>
        public void Turn(float right, float up)
        {
            _yaw += right;
            _pitch = Limit(_pitch, up, PitchMin, PitchMax);
            Apply();
        }

        /// <summary>A mouse drag across the pane turns it. A drag over its whole height is a full circle.</summary>
        public void Drag(Vector2 pixels, float viewHeight)
        {
            var k = 360f / Mathf.Max(1f, viewHeight);
            Turn(pixels.x * k * DragSpeed, pixels.y * k * DragSpeed);
        }

        /// <summary>
        /// First person: every mouse move turns the view, the way the game's mouse look does
        /// (PlayerController.LateUpdate): 0.05 degrees a pixel times the game's Mouse sensitivity,
        /// with its invert setting. No setting of the mod's. A jump bigger than a hand is clipped.
        /// </summary>
        public void MouseLook(Vector2 pixels)
        {
            if (pixels == Vector2.zero)
            {
                return;
            }

            var step = LookStep * (ZInput.IsGamepadMouseActive() ? PlayerController.m_switchMouseSens : PlayerController.m_mouseSens);
            Turn(Mathf.Clamp(pixels.x, -LookJump, LookJump) * step,
                Mathf.Clamp(pixels.y, -LookJump, LookJump) * step * (PlayerController.m_invertMouse ? -1f : 1f));
        }

        /// <summary>Moves in the view plane by pane pixels, so the point <paramref name="depth"/> away follows the mouse.</summary>
        public void Pan(Vector2 pixels, float depth, float viewHeight)
        {
            // Without perspective every depth scales the same: the pivot's.
            var k = 2f * (_ortho ? _dist : depth) * Mathf.Tan(_camera.Unity.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, viewHeight);
            _pos += Right * (-pixels.x * k) + Up * (-pixels.y * k);
            Apply();
        }

        /// <summary>
        /// Wheel zoom: 5% of the way per notch, toward the pane point the caller gives. The caller
        /// aims at the cursor, or at the middle of the view once the pane has the mouse.
        ///
        /// <see cref="Distance"/> follows the wheel, because <see cref="Pivot"/>, the pan depth and
        /// the pad's framing all read it.
        /// </summary>
        public void Zoom(float delta, Vector2 viewport)
        {
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            var scale = Mathf.Pow(0.95f, Mathf.Abs(delta) * 0.01f);
            var next = Mathf.Clamp(delta < 0f ? _dist * scale : _dist / scale, NearestDistance, FarthestDistance);
            if (_ortho)
            {
                // Zooming is the size of the box the camera sees: the camera stays on its pivot's line.
                var pivot = Pivot;
                _dist = next;
                _pos = pivot - (Forward * _dist);
                Apply();
                return;
            }

            _pos += LocalDirection(viewport) * (_dist - next);
            _dist = next;
            Apply();
        }

        /// <summary>Keys: forward/back, left/right, up/down, each -1..1. Shift makes it three times faster.</summary>
        public void FlyKeys(Vector3 wish, bool boost, float dt)
        {
            Fly(wish, FlySpeed * (boost ? FlyBoost : 1f), dt);
        }

        /// <summary>The pad's left stick flies, the right stick turns.</summary>
        public void FlyPad(Vector3 wish, bool boost, float dt)
        {
            Fly(wish, PadFlySpeed * (boost ? FlyBoost : 1f), dt);
        }

        /// <summary>
        /// The game's own right-stick look: 110 degrees a second at full stick, times the game's
        /// Gamepad sensitivity, with its invert settings. No setting of the mod's.
        /// </summary>
        public void TurnPad(Vector2 stick, float dt)
        {
            if (stick == Vector2.zero)
            {
                return;
            }

            var step = PadTurn * PlayerController.m_gamepadSens * dt;
            Turn(stick.x * step * (PlayerController.m_invertCameraX ? -1f : 1f),
                stick.y * step * (PlayerController.m_invertCameraY ? -1f : 1f));
        }

        private void Fly(Vector3 wish, float speed, float dt)
        {
            if (wish.sqrMagnitude == 0f)
            {
                return;
            }

            var by = (Forward * wish.z + Right * wish.x + Vector3.up * wish.y) * (speed * dt);
            Move(by);
        }

        /// <summary>The way the camera looks through a point of the pane, in the scene's own space.</summary>
        private Vector3 LocalDirection(Vector2 viewport)
        {
            var ray = _camera.Unity.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            var parent = _camera.Transform != null ? _camera.Transform.parent : null;
            return parent != null ? parent.InverseTransformDirection(ray.direction) : ray.direction;
        }

        private void Apply()
        {
            var transform = _camera.Transform;
            if (transform != null)
            {
                transform.localPosition = _pos;
                transform.localRotation = Quaternion.Euler(-_pitch, _yaw, 0f);
            }

            var unity = _camera.Unity;
            if (unity != null)
            {
                unity.orthographic = _ortho;

                // Flat view: the camera sits only as far back as the zoom, so what is nearer would be cut
                // off. A negative near plane keeps the whole blueprint, in front of the camera and behind.
                unity.nearClipPlane = _ortho ? -FarthestDistance : Mathf.Max(0.02f, _near);

                // The box the camera sees is the one the perspective camera shows at the pivot.
                unity.orthographicSize = Mathf.Max(0.05f, _dist * Mathf.Tan(unity.fieldOfView * 0.5f * Mathf.Deg2Rad));
            }
        }

        /// <summary>Adds to a pitch, inside the range. A pitch already outside it only moves back toward it.</summary>
        private static float Limit(float pitch, float by, float low, float high)
        {
            return Mathf.Clamp(pitch + by, Mathf.Min(low, pitch), Mathf.Max(high, pitch));
        }
    }
}
