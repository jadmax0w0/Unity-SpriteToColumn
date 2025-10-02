using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    public class ImageToColumnGenManager : MonoBehaviour
    {
        public void GenerateColumns()
        {
            StartCoroutine(GenerateColumnForAll());
        }
        
        private IEnumerator GenerateColumnForAll()
        {
            var allGenerators = FindObjectsOfType<ImageToColumnGenerator>(false);
            var columnAbleComponents = new List<ImageToColumnGenerator>();
            foreach (var generator in allGenerators)
            {
                var skip = !generator.gameObject.activeInHierarchy || IsColumnGenerated(generator);
                if (skip) continue;
                columnAbleComponents.Add(generator);
            }

            // 生成某个的柱体之前，要把其他所有的全禁掉
            for (int i = 0; i < columnAbleComponents.Count; i++)
            {
                for (int j = 0; j < columnAbleComponents.Count; j++)
                    columnAbleComponents[j].gameObject.SetActive(j == i);
                
                var component = columnAbleComponents[i];
                
                // Set to default transform parameters
                var position = component.transform.position;
                var rotation = component.transform.rotation;
                var localScale = component.transform.localScale;
                
                component.transform.position = Vector3.zero;
                component.transform.rotation = Quaternion.identity;
                component.transform.localScale = Vector3.one;

                yield return new WaitForFixedUpdate();
                
                component.GenerateColumnFromImage();

                yield return new WaitForFixedUpdate();
                
                component.transform.position = position;
                component.transform.rotation = rotation;
                component.transform.localScale = localScale;

                yield return new WaitForFixedUpdate();
            }

            foreach (var component in columnAbleComponents)
            {
                component.gameObject.SetActive(true);
            }
        }

        private static bool IsColumnGenerated(ImageToColumnGenerator component)
        {
            if (component.enabled == false)
                return true;
            
            var go = component.gameObject;
            var childCount = go.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                if (go.transform.GetChild(i).name == ImageToColumnGenerator.ColumnChildObjectName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}