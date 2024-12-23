// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    internal static class MoonObjectHelper
    {
        public const int NOTFOUND = -1;

        /// <summary>
        /// Searches through the array and finds the array position of item most similar to the one provided.
        /// </summary>
        /// <typeparam name="T">Only objects that extend from the SongObject class.</typeparam>
        /// <param name="searchItem">The item you want to search for.</param>
        /// <param name="objects">The items you want to search through.</param>
        /// <returns>Returns the array position of the object most similar to the search item provided in the 'objects' parameter. 
        /// Returns SongObjectHelper.NOTFOUND if there are no objects provided. </returns>
        public static int FindClosestPosition<T>(T searchItem, IList<T> objects) where T : MoonObject
        {
            int lowerBound = 0;
            int upperBound = objects.Count - 1;
            int index = NOTFOUND;

            int midPoint;

            while (lowerBound <= upperBound)
            {
                midPoint = (lowerBound + upperBound) / 2;
                index = midPoint;

                if (objects[midPoint].InsertionEquals(searchItem))
                {
                    break;
                }
                else
                {
                    if (objects[midPoint].InsertionCompareTo(searchItem) < 0)
                    {
                        // data is in upper half
                        lowerBound = midPoint + 1;
                    }
                    else
                    {
                        // data is in lower half 
                        upperBound = midPoint - 1;
                    }
                }
            }
            return index;
        }

        /// <summary>
        /// Searches through the array and finds the array position of item with the closest position to the one provided.
        /// </summary>
        /// <typeparam name="T">Only objects that extend from the SongObject class.</typeparam>
        /// <param name="searchItem">The item you want to search for.</param>
        /// <param name="objects">The items you want to search through.</param>
        /// <returns>Returns the array position of the closest object located at the specified tick position. 
        /// Returns SongObjectHelper.NOTFOUND if there are no objects provided. </returns>
        public static int FindClosestPosition<T>(uint position, IList<T> objects) where T : MoonObject
        {
            int lowerBound = 0;
            int upperBound = objects.Count - 1;
            int index = NOTFOUND;

            int midPoint;

            while (lowerBound <= upperBound)
            {
                midPoint = (lowerBound + upperBound) / 2;
                index = midPoint;

                if (objects[midPoint].tick == position)
                {
                    break;
                }
                else
                {
                    if (objects[midPoint].tick < position)
                    {
                        // data is in upper half
                        lowerBound = midPoint + 1;
                    }
                    else
                    {
                        // data is in lower half 
                        upperBound = midPoint - 1;
                    }
                }
            }

            return index;
        }

        public static int FindClosestPositionRoundedDown<T>(uint tick, IList<T> objects) where T : MoonObject
        {
            int index = FindClosestPosition(tick, objects);

            if (index > 0 && objects[index].tick > tick)
                --index;

            return index;
        }

        /// <summary>
        /// Searches through the array to collect all the items found at the specified position.
        /// </summary>
        /// <typeparam name="T">Only objects that extend from the SongObject class.</typeparam>
        /// <param name="position">The tick position of the items.</param>
        /// <param name="objects">The list you want to search through.</param>
        /// <returns>Returns an array of items located at the specified tick position. 
        /// Returns an empty array if no items are at that exact tick position. </returns>
        public static void FindObjectsAtPosition<T>(uint position, IList<T> objects, out int startIndex, out int length) where T : MoonObject
        {
            int index = FindClosestPosition(position, objects);
            startIndex = 0;
            length = 0;

            if (index != NOTFOUND && objects[index].tick == position)
            {
                int lowRange = index, highRange = index;

                while (lowRange > 0 && objects[index].tick == objects[lowRange - 1].tick)
                {
                    --lowRange;
                }

                while (highRange < objects.Count - 1 && objects[index].tick == objects[highRange + 1].tick)
                {
                    ++highRange;
                }

                length = highRange - lowRange + 1;
                startIndex = lowRange;

                //T[] objectSelection = new T[length];
                //System.Array.Copy(objects, lowRange, objectSelection, 0, length);
                //
                //return objectSelection;
            }
        }

        /// <summary>
        /// Searches through the provided array to find the item specified.  
        /// </summary>
        /// <typeparam name="T">Only objects that extend from the SongObject class.</typeparam>
        /// <param name="searchItem">The item you want to search for.</param>
        /// <param name="objects">The items you want to search through.</param>
        /// <returns>Returns the array position that the search item was found at within the objects array. 
        /// Returns SongObjectHelper.NOTFOUND if the item does not exist in the objects array. </returns>
        public static int FindObjectPosition<T>(T searchItem, IList<T> objects) where T : MoonObject
        {
            int pos = FindClosestPosition(searchItem, objects);

            if (pos != NOTFOUND && objects[pos] != searchItem)
            {
                pos = NOTFOUND;
            }

            return pos;
        }

        public static int FindObjectPosition<T>(uint position, IList<T> objects) where T : MoonObject
        {
            int pos = FindClosestPosition(position, objects);

            if (pos != NOTFOUND && objects[pos].tick != position)
            {
                pos = NOTFOUND;
            }

            return pos;
        }

        private static int FindPreviousPosition<T>(System.Type type, int startPosition, IList<T> list) where T : MoonObject
        {
            // Linear search
            if (startPosition < 0 || startPosition > list.Count - 1)
                return NOTFOUND;
            else
            {
                --startPosition;

                while (startPosition >= 0)
                {
                    if (list[startPosition].GetType() == type)
                        return startPosition;
                    --startPosition;
                }

                return NOTFOUND;
            }
        }

        private static T? FindPreviousOfType<T>(System.Type type, int startPosition, IList<T> list) where T : MoonObject
        {
            int pos = FindPreviousPosition(type, startPosition, list);

            if (pos == NOTFOUND)
                return null;
            else
                return list[pos];
        }

        private static int FindNextPosition<T>(System.Type type, int startPosition, IList<T> list) where T : MoonObject
        {
            // Linear search
            if (startPosition < 0 || startPosition > list.Count - 1)
                return NOTFOUND;
            else
            {
                ++startPosition;

                while (startPosition < list.Count)
                {
                    if (list[startPosition].GetType() == type)
                        return startPosition;
                    ++startPosition;
                }

                return NOTFOUND;
            }
        }

        private static T? FindNextOfType<T>(System.Type type, int startPosition, IList<T> list) where T : MoonObject
        {
            int pos = FindNextPosition(type, startPosition, list);
            if (pos == NOTFOUND)
                return null;
            else
                return list[pos];
        }


        /// <summary>
        /// Pushes a MoonNote to the back of a list, ensuring correct setting of `previous` & `next` variables.
        /// </summary>
        /// <param name="note">The note to be inserted, with assumed correct ordering.</param>
        /// <param name="notes">The list in which the note will be inserted.</param>
        /// <returns>Returns the list position it was inserted into,
        /// which equates to the list size before insertion.</returns>
        public static int PushNote(MoonNote note, List<MoonNote> notes)
        {
            int pos = notes.Count;
            if (pos > 0)
            {
                note.previous = notes[pos - 1];
                notes[pos - 1].next = note;
            }
            notes.Add(note);
            return pos;
        }

        /// <summary>
        /// Insert a note into the provided list linearly, with the search starting from the back of the list.
        /// Ensures correct setting of 'previous' and 'next' variables.
        /// Use for handling .mid note off events
        /// </summary>
        /// <param name="note">The note to be inserted.</param>
        /// <param name="notes">The list in which the note will be inserted.</param>
        /// <returns>Returns the list position it was inserted into.</returns>
        public static int OrderedInsertFromBack(MoonNote note, List<MoonNote> notes)
        {
            int pos = notes.Count;
            while (pos > 0 && notes[pos - 1].tick > note.tick)
                --pos;

            if (pos > 0 && notes[pos - 1].tick == note.tick)
            {
                int check = pos - 1;
                do
                {
                    if (notes[check].InsertionEquals(note))
                        return check;
                    --check;
                } while (check >= 0 && notes[check].tick == note.tick);
            }

            if (pos > 0)
            {
                note.previous = notes[pos - 1];
                notes[pos - 1].next = note;
            }

            if (pos < notes.Count)
            {
                notes[pos].previous = note;
                note.next = notes[pos];
            }
            notes.Insert(pos, note);
            return pos;
        }

        /// <summary>
        /// Insert an item into the provided list linearly, with the search starting from the back of the list.
        /// Use for handling .mid note off events
        /// </summary>
        /// <param name="item">The item to be inserted.</param>
        /// <param name="list">The list in which the item will be inserted.</param>
        /// <returns>Returns the list position it was inserted into.</returns>
        public static int OrderedInsertFromBack<T>(T item, List<T> list) where T : MoonObject
        {
            int pos = list.Count;
            while (pos > 0 && item.tick < list[pos - 1].tick)
                --pos;

            if (pos > 0 && list[pos - 1].tick == item.tick)
            {
                int check = pos - 1;
                do
                {
                    if (list[check].InsertionEquals(item))
                        return check;
                    --check;
                } while (check >= 0 && list[check].tick == item.tick);
            }

            list.Insert(pos, item);
            return pos;
        }

        /// <summary>
        /// Adds the item into a sorted position into the specified list and updates the note linked list if a note is inserted. 
        /// </summary>
        /// <typeparam name="T">Only objects that extend from the SongObject class.</typeparam>
        /// <param name="item">The item to be inserted.</param>
        /// <param name="list">The list in which the item will be inserted.</param>
        /// <returns>Returns the list position it was inserted into.</returns>
        public static int Insert<T>(T item, List<T> list) where T : MoonObject
        {
            int insertionPos = NOTFOUND;
            int count = list.Count;

            if (count > 0)
            {
                if (list[count - 1].InsertionCompareTo(item) < 0)
                {
                    insertionPos = count;
                    list.Add(item);
                }
                else if (list[count - 1].tick == item.tick && item.classID == list[count - 1].classID)
                {
                    // Linear search backwards
                    int pos = count - 1;
                    while (pos >= 0 && list[pos].InsertionCompareTo(item) >= 0)        // Find the next item less than the current one and insert into the position after that
                        --pos;

                    insertionPos = pos + 1;

                    // Account for overwrite
                    if (insertionPos < count && list[insertionPos].InsertionEquals(item))
                    {
                        list[insertionPos] = item;
                    }
                    else
                        list.Insert(insertionPos, item);
                }
                else
                {
                    insertionPos = FindClosestPosition(item, list);

                    if (insertionPos != NOTFOUND)
                    {
                        if (list[insertionPos].InsertionEquals(item) && item.classID == list[insertionPos].classID)
                        {
                            list[insertionPos] = item;
                        }
                        // Insert into sorted position
                        else
                        {
                            if (item.InsertionCompareTo(list[insertionPos]) > 0)
                            {
                                ++insertionPos;
                            }
                            list.Insert(insertionPos, item);
                        }
                    }
                }
            }

            if (insertionPos == NOTFOUND)
            {
                // Adding the first note
                list.Add(item);
                insertionPos = list.Count - 1;
            }

            if ((MoonObject.ID)item.classID == MoonObject.ID.Note && list[insertionPos] is MoonNote current)
            {
                // Update linked list
                var previous = FindPreviousOfType(typeof(MoonNote), insertionPos, list) as MoonNote;
                var next = FindNextOfType(typeof(MoonNote), insertionPos, list) as MoonNote;

                current.previous = previous;
                if (previous != null)
                    previous.next = current;

                current.next = next;
                if (next != null)
                    next.previous = current;

                // Update flags depending on open notes
                //Note.Flags flags = current.flags;
                //previous = current.previous;
                //next = current.next;
                //
                //Note openNote = null;
                ////bool openFound = false;
                //bool standardFound = false;
                //
                //// Collect all the flags
                //while (previous != null && previous.tick == current.tick)
                //{
                //    if (previous.IsOpenNote())
                //        openNote = previous;
                //    else
                //        standardFound = true;
                //
                //    flags |= previous.flags;
                //    previous = previous.previous;
                //}
                //
                //while (next != null && next.tick == current.tick)
                //{
                //    if (next.IsOpenNote())
                //        openNote = next;
                //    else
                //        standardFound = true;
                //
                //    flags |= next.flags;
                //    next = next.next;
                //}
                //
                //// Apply flags
                //if (!current.IsOpenNote() && openNote != null)
                //{
                //    //openNote.controller.Delete();
                //}
                //else if (current.IsOpenNote() && standardFound)
                //{ }
                //else
                //{
                //    current.flags = flags;
                //
                //    previous = current.previous;
                //    next = current.next;
                //    while (previous != null && previous.tick == current.tick)
                //    {
                //        previous.flags = flags;
                //        previous = previous.previous;
                //    }
                //
                //    while (next != null && next.tick == current.tick)
                //    {
                //        next.flags = flags;
                //        next = next.next;
                //    }
                //}
            }

            return insertionPos;
        }

        /// <summary>
        /// Removes the item from the specified list and updates the note linked list if a note is removed. 
        /// </summary>
        /// <typeparam name="T">Only objects that extend from the SongObject class.</typeparam>
        /// <param name="item">The item to be remove.</param>
        /// <param name="list">The list in which the item will be removed from.</param>
        /// <returns>Returns whether the item was successfully removed or not (may not be removed if the objects was not found).</returns>
        public static bool Remove(MoonNote item, IList<MoonNote> list, bool uniqueData = true)
        {
            int pos = FindObjectPosition(item, list);

            if (pos != NOTFOUND)
            {
                if (uniqueData)
                {
                    // Update linked list
                    var previous = FindPreviousOfType(item.GetType(), pos, list);
                    var next = FindNextOfType(item.GetType(), pos, list);

                    if (previous != null)
                        previous.next = next;

                    if (next != null)
                        next.previous = previous;
                }
                list.RemoveAt(pos);

                return true;
            }

            return false;
        }

        public static bool Remove<T>(T item, IList<T> list) where T : MoonObject
        {
            int pos = FindObjectPosition(item, list);
            if (pos != NOTFOUND)
            {
                list.RemoveAt(pos);
                return true;
            }
            return false;
        }


        public static T[] GetRangeCopy<T>(T[] list, uint minPos, uint maxPos) where T : MoonObject
        {
            GetRange(list, minPos, maxPos, out int index, out int length);

            var rangedList = new T[length];
            System.Array.Copy(list, index, rangedList, 0, rangedList.Length);

            return rangedList;
        }

        /// <summary>
        /// Gets a collection of items between a minimum and maximum tick position range.
        /// </summary>
        /// <typeparam name="T">Only objects that extend from the SongObject class.</typeparam>
        /// <param name="list">The list to search through.</param>
        /// <param name="minPos">The minimum range (inclusive).</param>
        /// <param name="maxPos">The maximum range (inclusive).</param>
        /// <returns>Returns all the objects found between the minimum and maximum tick positions specified.</returns>
        public static void GetRange<T>(IList<T> list, uint minPos, uint maxPos, out int index, out int length) where T : MoonObject
        {
            index = 0;
            length = 0;

            if (minPos > maxPos || list.Count < 1)
                return;

            int minArrayPos = FindClosestPosition(minPos, list);
            int maxArrayPos = FindClosestPosition(maxPos, list);

            if (minArrayPos == NOTFOUND || maxArrayPos == NOTFOUND)
                return;
            else
            {
                // Find position may return an object located at a lower position than the minimum position
                while (minArrayPos < list.Count && list[minArrayPos].tick < minPos)
                {
                    ++minArrayPos;
                }

                if (minArrayPos > list.Count - 1)
                    return;

                // Iterate to the very first object at a greater position, as there may be multiple objects located at the same position
                while (minArrayPos - 1 >= 0 && list[minArrayPos - 1].tick >= minPos)
                {
                    --minArrayPos;
                }

                // Find position may return an object locationed at a greater position than the maximum position
                while (maxArrayPos >= 0 && list[maxArrayPos].tick > maxPos)
                {
                    --maxArrayPos;
                }

                if (maxArrayPos < 0)
                    return;

                // Iterate to the very last object at a lesser position, as there may be multiple objects located at the same position
                while (maxArrayPos + 1 < list.Count && list[maxArrayPos + 1].tick <= maxPos)
                {
                    ++maxArrayPos;
                }

                if (minArrayPos > maxArrayPos)
                    return;

                index = minArrayPos;
                length = maxArrayPos - minArrayPos + 1;

                if (list[minArrayPos].tick < minPos || list[maxArrayPos].tick > maxPos)
                    length = 0;
            }
        }

        public static void sort<T>(T[] songObjects) where T : MoonObject
        {
            int j;
            T temp;
            for (int i = 1; i < songObjects.Length; i++)
            {
                temp = songObjects[i];
                j = i - 1;

                while (j >= 0 && songObjects[j].InsertionCompareTo(temp) > 0)
                {
                    songObjects[j + 1] = songObjects[j];
                    j--;
                }

                songObjects[j + 1] = temp;
            }
        }

        public static int GetIndexOfPrevious<T>(IList<T> songObjects, uint position) where T : MoonObject
        {
            int closestPos = FindClosestPosition(position, songObjects);
            if (closestPos != NOTFOUND)
            {
                // Select the smaller of the two
                if (songObjects[closestPos].tick <= position)
                    return closestPos;
                else if (closestPos > 0)
                    return closestPos - 1;
                else
                    return NOTFOUND;
            }

            return closestPos;
        }

        public static int GetIndexOfNext<T>(IList<T> songObjects, uint position) where T : MoonObject
        {
            int closestPos = FindClosestPosition(position, songObjects);
            if (closestPos != NOTFOUND)
            {
                // Select the larger of the two
                if (songObjects[closestPos].tick >= position)
                    return closestPos;
                else if (closestPos < songObjects.Count - 1)
                    return closestPos + 1;
                else
                    return NOTFOUND;
            }

            return closestPos;
        }

        public static T? GetPrevious<T>(IList<T> songObjects, uint position) where T : MoonObject
        {
            int pos = GetIndexOfPrevious(songObjects, position);
            if (pos != NOTFOUND)
                return songObjects[pos];
            else
                return null;
        }

        public static T? GetPreviousNonInclusive<T>(IList<T> songObjects, uint position) where T : MoonObject
        {
            int pos = GetIndexOfPrevious(songObjects, position);
            if (pos != NOTFOUND)
            {
                if (songObjects[pos].tick == position && pos > 0)
                    --pos;

                return songObjects[pos];
            }
            else
                return null;
        }

        public static T? GetNextNonInclusive<T>(IList<T> songObjects, uint position) where T : MoonObject
        {
            int pos = GetIndexOfNext(songObjects, position);
            if (pos != NOTFOUND)
            {
                if (songObjects[pos].tick == position && pos < songObjects.Count - 1)
                    ++pos;

                return songObjects[pos];
            }
            else
                return null;
        }
    }
}
