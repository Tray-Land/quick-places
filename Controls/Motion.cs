using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace QuickPlaces.Controls;

/// <summary>
/// Small composition animations for the flyout's content: staggered entrances, springy hover and
/// press, a pop-in, and a loading shimmer. Everything runs on the compositor thread and is a
/// no-op when the user turned animations off in Windows (Accessibility > Visual effects).
/// </summary>
internal static class Motion
{
    private static readonly UISettings s_uiSettings = new();

    public static bool IsEnabled => s_uiSettings.AnimationsEnabled;

    /// <summary>
    /// Fades and slides elements up into place one after another. Call after they become visible;
    /// <paramref name="startDelayMs"/> leaves room for a connected animation to lead.
    /// </summary>
    public static void StaggerIn(IEnumerable<UIElement> elements, int startDelayMs = 0, int stepMs = 40, float distance = 16)
    {
        int index = 0;
        foreach (UIElement element in elements)
        {
            if (element.Visibility == Visibility.Visible)
            {
                SlideIn(element, TimeSpan.FromMilliseconds(startDelayMs + (index++ * stepMs)), distance);
            }
        }
    }

    public static void SlideIn(UIElement element, TimeSpan delay, float distance = 16, float fromX = 0)
    {
        if (!IsEnabled)
        {
            return;
        }

        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;

        // Hold the start frame through the delay so nothing flashes at its resting place first.
        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1, compositor.CreateCubicBezierEasingFunction(new(0.1f, 0.9f), new(0.2f, 1f)));
        fade.Duration = TimeSpan.FromMilliseconds(300);
        fade.DelayTime = delay;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        Vector3KeyFrameAnimation slide = compositor.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(0, new Vector3(fromX, distance, 0));
        slide.InsertKeyFrame(1, Vector3.Zero, compositor.CreateCubicBezierEasingFunction(new(0.1f, 0.9f), new(0.2f, 1f)));
        slide.Duration = TimeSpan.FromMilliseconds(420);
        slide.DelayTime = delay;
        slide.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Translation", slide);
    }

    /// <summary>Scales an element up from small with a soft overshoot, like a badge appearing.</summary>
    public static void PopIn(UIElement element, TimeSpan delay = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        Visual visual = ElementCompositionPreview.GetElementVisual(element);

        // Just made visible: there's no size to center on until layout runs, so wait for it.
        if (element.ActualSize == Vector2.Zero && element is FrameworkElement fe)
        {
            visual.Opacity = 0;
            void OnSized(object sender, SizeChangedEventArgs e)
            {
                fe.SizeChanged -= OnSized;
                PopIn(element, delay);
            }

            fe.SizeChanged += OnSized;
            return;
        }

        CenterOn(element, visual);
        Compositor compositor = visual.Compositor;

        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new Vector3(0.6f, 0.6f, 1));
        scale.InsertKeyFrame(1, Vector3.One, compositor.CreateCubicBezierEasingFunction(new(0.3f, 1.5f), new(0.6f, 1f)));
        scale.Duration = TimeSpan.FromMilliseconds(450);
        scale.DelayTime = delay;
        scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1);
        fade.Duration = TimeSpan.FromMilliseconds(200);
        fade.DelayTime = delay;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Scale", scale);
        visual.StartAnimation("Opacity", fade);
    }

    /// <summary>Springs an element to <paramref name="scale"/> around its center (hover, press).</summary>
    public static void SpringScale(UIElement element, float scale)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        CenterOn(element, visual);
        if (!IsEnabled)
        {
            visual.Scale = new Vector3(scale, scale, 1);
            return;
        }

        SpringVector3NaturalMotionAnimation spring = visual.Compositor.CreateSpringVector3Animation();
        spring.FinalValue = new Vector3(scale, scale, 1);
        spring.DampingRatio = 0.55f;
        spring.Period = TimeSpan.FromMilliseconds(45);
        visual.StartAnimation("Scale", spring);
    }

    /// <summary>
    /// Hover lifts <paramref name="target"/> slightly and a press pushes it in, both on springs.
    /// <paramref name="source"/> receives the pointer (often a larger hit area than the target).
    /// </summary>
    public static void AddPointerSpring(UIElement source, UIElement target, float hover = 1.08f, float pressed = 0.92f)
    {
        source.PointerEntered += (_, _) => SpringScale(target, hover);
        source.PointerExited += (_, _) => SpringScale(target, 1f);
        source.PointerCanceled += (_, _) => SpringScale(target, 1f);
        source.PointerCaptureLost += (_, _) => SpringScale(target, 1f);
        source.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => SpringScale(target, pressed)), true);
        source.AddHandler(UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => SpringScale(target, hover)), true);
    }

    /// <summary>Pulses an element's opacity until <see cref="StopShimmer"/>: the skeleton loading look.</summary>
    public static void StartShimmer(UIElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        if (!IsEnabled)
        {
            visual.Opacity = 0.7f;
            return;
        }

        ScalarKeyFrameAnimation pulse = visual.Compositor.CreateScalarKeyFrameAnimation();
        pulse.InsertKeyFrame(0, 1f);
        pulse.InsertKeyFrame(0.5f, 0.45f);
        pulse.InsertKeyFrame(1, 1f);
        pulse.Duration = TimeSpan.FromMilliseconds(1200);
        pulse.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation("Opacity", pulse);
    }

    public static void StopShimmer(UIElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Opacity");
        visual.Opacity = 1f;
    }

    /// <summary>Clears any entrance state so an element shows at rest (used when skipping animation).</summary>
    public static void Reset(UIElement element)
    {
        // "Translation" only exists once enabled; stopping it on a fresh element throws.
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Opacity");
        visual.StopAnimation("Translation");
        visual.StopAnimation("Scale");
        visual.Opacity = 1f;
        visual.Scale = Vector3.One;
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
    }

    private static void CenterOn(UIElement element, Visual visual)
    {
        Vector2 size = element.ActualSize;
        visual.CenterPoint = new Vector3(size.X / 2, size.Y / 2, 0);
    }
}
