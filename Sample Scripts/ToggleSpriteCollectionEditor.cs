using ...

/// This is a class I wrote myself for tooling editors I am using both at my job as well as in my private game project.
/// I am using an abstract base class from a bought API with a blueprint that allows for setting values of dynamic types to register property editor classes for said types.
/// 
/// Thereby, I am working with Undo/Redo stacking, reflection, attributing and Model-View-View-Model techniques.
/// A graphical CSS-like element is linked, in a way that instantiating it attaches this class to it.
/// 
/// Goal: Display a range of clickable sprites (toggles), whereas sprites are defined in attributes of the underlying enum entries.
/// When the user clicks one of the sprites, the represented enum entry is selected and passed to the data model.
namespace ZukiniFun.Toolings
{
    /// <summary>
    /// Custom alternative property editor for enums.
    /// The goal is to have clickable elements, most likely in form of images, with a label, instead of a dropdown.
    /// </summary>
    public class ToggleSpriteCollectionEditor : ConvertablePropertyEditor<Enum>
    {
        /// <summary>
        /// Prefab to be instantiated.
        /// </summary>
        [SerializeField]
        private GameObject _toggleWithSpritePrefab;

        /// <summary>
        /// Group elements for controlling selection status of the instantiated toggles in dependence to each other.
        /// </summary>
        [SerializeField]
        private ToggleGroup _toggleGroup;

        /// <summary>
        /// Parent where the dynamically instantiated toggles are attached to.
        /// </summary>
        [SerializeField]
        private Transform _toggleContainer;

        /// <summary>
        /// Stores all Toggle elements which have been instantiated.
        /// </summary>
        private List<Toggle> _toggleCollection = new List<Toggle>();

        /// <summary>
        /// Stores access to custom toggle functionalities like visual representation of toggles through images and labels.
        /// </summary>
        private List<ToggleCreatorExtension> _toggleExtensions = new List<ToggleCreatorExtension>();

        /// <summary>
        /// The type of the enum which is being displayed by this property editor.
        /// </summary>
        private Type _enumType;

        /// <summary>
        /// The entries of said enum.
        /// </summary>
        private Array _enumValues;

        /// <summary>
        /// Click events are attached.
        /// Manual input field initialization is invoked.
        /// Thereby the size of the collection depends on the specific enum passed with the reflected type.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="accessor"></param>
        /// <param name="memberInfo"></param>
        /// <param name="eraseTargetCallback"></param>
        /// <param name="label"></param>
        protected override void InitOverride(object[] target, object[] accessor, MemberInfo memberInfo, Action<object, object> eraseTargetCallback = null, string label = null)
        {
            base.InitOverride(target, accessor, memberInfo, eraseTargetCallback, label);

            Enum initValue = GetValue();
            _enumType = GetEnumType(accessor);
            _enumValues = Enum.GetValues(initValue.GetType());

            foreach (var value in Enum.GetValues(initValue.GetType()))
            {
                GameObject spawnedToggle = Instantiate(_toggleWithSpritePrefab, parent: _toggleContainer);
                spawnedToggle.name += Array.IndexOf(_enumValues, value);
                
                Toggle spawnedToggleComponent = spawnedToggle.GetComponent<Toggle>();
                if (spawnedToggleComponent != null)
                {
                    _toggleCollection.Add(spawnedToggleComponent);
                    spawnedToggleComponent.group = _toggleGroup;
                }

                ToggleCreatorExtension toggleExtension = spawnedToggle.GetComponent<ToggleCreatorExtension>();
                if (toggleExtension != null)
                {
                    _toggleExtensions.Add(toggleExtension);
                    toggleExtension.UserClickedCreatorToggle.AddListener(UserClickedToggle);
                }
            }

            List<EnumVisualResourceAttribute> attributeFields = GetCustomAttributeFieldsFromEnum();
            SetVisualResourcesToEditor(attributeFields);

            EditorValueChangedCallback();

            m_valueChangedCallback += EditorValueChangedCallback;
        }

        /// <summary>
        /// Events are de-attached.
        /// </summary>
        protected override void OnDestroyOverride()
        {
            base.OnDestroyOverride();

            m_valueChangedCallback -= EditorValueChangedCallback;

            foreach (var item in _toggleExtensions)
            {
                item.UserClickedCreatorToggle.RemoveListener(UserClickedToggle);
            }
        }

        /// <summary>
        /// User click callback which invokes manual setting the new value of this property editor.
        /// Includes Undo-Redo behavior definition.
        /// </summary>
        private void UserClickedToggle(Toggle eventToggle)
        {
            Editor.Undo.BeginRecord();

            Enum undoValue = GetValue();

            Toggle clickedToggle = _toggleCollection.Where(x => x == eventToggle).FirstOrDefault();
            int toggleIndex = _toggleCollection.IndexOf(clickedToggle);
            Enum newValue = (Enum)Enum.GetValues(_enumType).GetValue(toggleIndex);

            Editor.Undo.CreateRecord(
                redoCallback =>
                {
                    SetValue(newValue);
                    return true;
                },
                undoCallback =>
                {
                    SetValue(undoValue);
                    return true;
                });

            SetValue(newValue);

            Editor.Undo.EndRecord();
        }

        /// <summary>
        /// Manual adaption of Toggle appearences and interactibility.
        /// ToggleGroup controls that no more than one toggle can be set.
        /// </summary>
        /// <param name="locationValue"></param>
        private void EditorValueChangedCallback()
        {
            Enum currentValue = GetValue();
            int selectedIndex = Array.IndexOf(_enumValues, currentValue);

            Toggle selectedToggle = _toggleCollection[selectedIndex];
            selectedToggle.isOn = true;
            selectedToggle.interactable = false;

            _toggleCollection.Where(x => x != selectedToggle).ToList().ForEach(x =>
            {
                x.isOn = false;
                x.interactable = true;
            });

            ActivityEditorCreator.CheckConditionalVisibility?.Invoke();
        }

        /// <summary>
        /// Upon initialization, the appearance of the sprite-toggles is adapted depending on the enum type.
        /// </summary>
        private List<EnumVisualResourceAttribute> GetCustomAttributeFieldsFromEnum()
        {
            List<EnumVisualResourceAttribute> visualResources = new List<EnumVisualResourceAttribute>();

            foreach (var value in _enumValues)
            {
                FieldInfo fieldInfo = value.GetType().GetField(value.ToString());

                EnumVisualResourceAttribute attribute = fieldInfo.GetCustomAttribute<EnumVisualResourceAttribute>();
                if (attribute != null)
                {
                    visualResources.Add(attribute);
                }
            }

            return visualResources;
        }

        /// <summary>
        /// Return the enum-type of the passed object which is assumed to be an enum.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private Type GetEnumType(object target)
        {
            CustomTypeFieldAccessor[] fieldAccessors = target as CustomTypeFieldAccessor[];
            if (fieldAccessors != null && fieldAccessors.Length > 0 && fieldAccessors[0] != null)
            {
                return fieldAccessors[0].Type;
            }
            else
            {
                CustomTypeFieldAccessor fieldAccessor = target as CustomTypeFieldAccessor;
                if (fieldAccessor != null)
                {
                    return fieldAccessor.Type;
                }
                else
                {
                    return MemberInfoType;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resourceAttributeCollections"></param>
        private void SetVisualResourcesToEditor(List<EnumVisualResourceAttribute> resourceAttributeCollections)
        {
            for (int i = 0; i < resourceAttributeCollections.Count; i++)
            {
                EnumVisualResourceAttribute collection = resourceAttributeCollections[i];

                _toggleExtensions[i].SetToggleSpriteFromResources(collection.toggleSprite);
                _toggleExtensions[i].SetToggleLabelI2Key(collection.translationKey);
            }
        }
    }
}
