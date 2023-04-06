using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Mackiloha.DTB
{
    public class ParentItem : Collection<DTBItem>, DTBItem
    {
        private ParentType _parentType;

        public ParentItem(ParentType type) : base()
        {
            if (!IsParentTypeValid(type)) throw new Exception("Invalid parent type!");
            _parentType = type;
        }

        public ParentItem(ParentType type, Collection<DTBItem> subItems) : base(subItems)
        {
            if (!IsParentTypeValid(type)) throw new Exception("Invalid parent type!");
            _parentType = type;
        }

        public ParentItem this[string keyword]
        {
            get
            {
                keyword = keyword.ToLower();
                return SearchForKeyword(this, keyword);
            }
        }

        private ParentItem SearchForKeyword(ParentItem parent, string keyword)
        {
            foreach (DTBItem item in parent)
            {
                if (item is ParentItem)
                {
                    ParentItem subParent = SearchForKeyword(item as ParentItem, keyword);
                    if (subParent != null) return subParent;
                }
                else if (item.NumericValue == 0x05) // Checks if it's a keyword type
                {
                    // Returns parent if found
                    if (((StringItem)item).String == keyword) return parent;
                }
            }

            return null;
        }

        public bool ContainsSameItems
        {
            get
            {
                if (Items == null || Items.Count < 1) return false;
                else
                {
                    int previousType = Items[0].NumericValue;

                    foreach (DTBItem item in Items)
                    {
                        if (item is ParentItem || previousType != item.NumericValue) return false;

                        previousType = item.NumericValue;
                    }

                    return true;
                }
            }
        }

        private bool IsParentTypeValid(ParentType type)
        {
            switch (type)
            {
                case ParentType.Default:
                case ParentType.Script:
                case ParentType.Property:
                    return true;
                default:
                    return false;
            }
        }

        public ParentType ParentType
        {
            get { return _parentType; }
            set
            {
                if (IsParentTypeValid(value)) _parentType = value;
                else throw new Exception("Invalid parent type!");
            }
        }

        public int NumericValue
        {
            get { return (int)_parentType; }
        }
    }

    public enum ParentType : int
    {
        /// <summary>
        /// Default "()"
        /// </summary>
        Default = 0x10,
        /// <summary>
        /// Script "{}"
        /// </summary>
        Script = 0x11,
        /// <summary>
        /// Property "[]"
        /// </summary>
        Property = 0x13
    }
}
