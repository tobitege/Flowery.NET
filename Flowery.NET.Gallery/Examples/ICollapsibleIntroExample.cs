namespace Flowery.NET.Gallery.Examples;

// A page whose intro (description and options) the category title bar can fold away to give the height to the demo.
public interface ICollapsibleIntroExample
{
    bool IsIntroVisible { get; set; }
}
