namespace DalaMock.Core.Imgui.Auto;

public class ImGuiElementGenerator
{
    private readonly IComponentContext componentContext;
    private readonly Dictionary<Type, IImGuiElement> imGuiElements;

    public ImGuiElementGenerator(IEnumerable<IImGuiElement> imGuiElements, IComponentContext componentContext)
    {
        this.componentContext = componentContext;
        this.imGuiElements = imGuiElements.ToDictionary(c => c.GetBackingType(), c => c);
    }

    public IEnumerable<IImGuiElement> GenerateElements(Type objectType)
    {
        List<IImGuiElement> elements = new List<IImGuiElement>();

        PropertyInfo[] properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (PropertyInfo property in properties)
        {
            Type propertyType = property.PropertyType;
            if (this.imGuiElements.ContainsKey(propertyType))
            {
                IImGuiElement elementType = this.imGuiElements[propertyType];
                var element = (IImGuiElement)this.componentContext.Resolve(elementType.GetType());
                var attribute = property.GetCustomAttribute(typeof(ImGuiGroupAttribute));
                if (attribute is ImGuiGroupAttribute groupAttribute)
                {
                    element.Group = groupAttribute.Name;
                }
                else
                {
                    element.Group = "Ungrouped";
                }

                element.GetMethodInfo = property.GetGetMethod();
                element.SetMethodInfo = property.GetSetMethod();
                element.Name = property.Name.AddSpaces();
                element.Id = property.Name;
                elements.Add(element);
            }
        }

        return elements;
    }
}
