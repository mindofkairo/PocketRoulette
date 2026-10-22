using System;
using EFT.Communications;
using EFT.UI;
using UnityEngine;

namespace PocketRoulette
{
    // Uses EFT's default prefab, animation and notification placement.
    internal sealed class JackpotNotification : InteractiveNotification, IDisposable
    {
        private string _description;
        private readonly bool _headline;
        private bool _closed;
        private BaseNotificationView _view;

        public JackpotNotification(string description, bool headline) : base(null)
        {
            _description = description; _headline = headline;
            Duration = ENotificationDurationType.Infinite;
            ShowImmediately = true;
        }

        public override string Description => _description;
        public override Color? TextColor => _headline ? new Color(1f, 0.84f, 0f) : new Color(0.8f, 0.85f, 1f);
        public override bool ShowNotification => !_closed;

        public override BaseNotificationView CreateView(INotificationViewFactory factory)
        {
            var view = base.CreateView(factory);
            _view = view;
            view.OnHideComplete += ViewHidden;
            // These are instructions, not a clickable action or an input-blocking panel.
            view._canvasGroup.interactable = false;
            view._canvasGroup.blocksRaycasts = false;
            // A delayed factory call must not leave an already-ended choice on screen.
            if (_closed) OnRemove.Invoke();
            return view;
        }

        public void UpdateText(string text)
        {
            if (_closed) return;
            _description = text;
            if (_view != null && _view.isActiveAndEnabled && _view.HasSameNotification(this)) _view._text.text = text;
        }

        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            var view = _view; _view = null;
            if (view != null)
            {
                view.OnHideComplete -= ViewHidden;
                // EFT pools views: never dismiss one which has since been reused.
                if (!view.isActiveAndEnabled || !view.HasSameNotification(this)) return;
            }
            OnRemove.Invoke();
        }

        private void ViewHidden(Notification notification, BaseNotificationView view)
        {
            view.OnHideComplete -= ViewHidden;
            if (ReferenceEquals(view, _view)) _view = null;
        }
    }
}
