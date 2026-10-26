using System;

namespace Flowery.NET.Gallery.Examples;

// A page whose intro (description and options) the category title bar can fold away to give the height to the demo.
public interface ICollapsibleIntroExample
{
    bool IsIntroVisible { get; set; }

    // Set while the category title bar folds the intro; the page then folds it itself where it needs the height, such as
    // for an editor.
    bool FoldsIntro { get; set; }

    // Raised when the intro is shown or folded.
    event EventHandler? IntroVisibilityChanged;
}
