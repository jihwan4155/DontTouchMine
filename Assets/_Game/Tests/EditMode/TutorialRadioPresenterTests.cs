using Game.Client.Tutorial;
using Game.Core.Settings;
using Game.Core.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class TutorialRadioPresenterTests
    {
        [Test]
        public void RadioUpdatesWithoutAUi_AndStopsAfterDisposal()
        {
            var root = new GameObject("Session");
            var general = new GeneralSettingsSystem(new InMemoryGeneralSettingsStore());
            using var locale = new UiLocale(general);
            try
            {
                var session = root.AddComponent<TutorialSession>();
                var view = new RecordingView();
                using var presenter = new TutorialRadioPresenter(view, session, locale);
                presenter.Start();
                Assert.That(view.Step, Is.EqualTo(TutorialStep.MoveAndLook));
                Assert.That(view.First, Does.Contain("신입"));
                Assert.That(view.Next, Is.EqualTo(TutorialRadioPresenter.Instruction(session.CurrentStep, "ko")));

                session.RetryCurrentStep();
                Assert.That(view.First, Does.Contain("같은 실수는 두 번"));
                general.Apply(new GeneralSettings("en"));
                Assert.That(view.First, Is.EqualTo(TutorialRadioPresenter.Instruction(session.CurrentStep, "en")));

                presenter.Dispose();
                var updates = view.Updates;
                session.RetryCurrentStep();
                general.Apply(new GeneralSettings("ko"));
                Assert.That(view.Updates, Is.EqualTo(updates));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private sealed class RecordingView : ITutorialRadioView
        {
            public TutorialStep Step;
            public string First;
            public string Next;
            public int Updates;
            public void HighlightStep(TutorialStep step) { Step = step; Updates++; }
            public void Show(string message) { First = message; Updates++; }
            public void ShowThen(string first, string next) { First = first; Next = next; Updates++; }
        }
    }
}
