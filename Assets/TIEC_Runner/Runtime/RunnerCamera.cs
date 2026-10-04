using UnityEngine;

namespace TIEC.Runner
{
    public sealed class RunnerCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] RunnerSession session;
        [SerializeField] Camera view;
        [SerializeField] Vector3 offset = new Vector3(0, 3.6f, 7);
        [SerializeField] float lookAhead = 10;
        public void Configure(Transform rider, RunnerSession owner, Camera camera)
        { target = rider; session = owner; view = camera; }
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            transform.LookAt(target.position + Vector3.up * 1.15f + Vector3.back * lookAhead);
            if (view != null) view.fieldOfView = 62;
        }
        void LateUpdate()
        {
            if (target == null || view == null) return;
            Vector3 desired = target.position + offset;
            desired.x = target.position.x * .6f;
            transform.position = Vector3.Lerp(transform.position, desired, 1 - Mathf.Exp(-8 * Time.deltaTime));
            transform.LookAt(target.position + Vector3.up * 1.15f + Vector3.back * lookAhead);
            float fov = 62 + (session != null ? Mathf.Clamp01(session.Speed / 15) * 10 : 0);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, fov, 1 - Mathf.Exp(-3 * Time.deltaTime));
        }
    }
}
