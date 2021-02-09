using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Menu;
using UnityEngine;
using RWCustom;
using System.IO;

namespace SlugBase
{
    using static CustomSceneManager;
    
    // Do not store any references to the owner MenuScene!
    internal class SceneEditor
    {
        // STATIC //

        private static AttachedField<MenuScene, SceneEditor> editor;

        public static void ApplyHooks() {
            On.Menu.MenuScene.GrafUpdate += MenuScene_GrafUpdate;
            On.Menu.MenuObject.RemoveSprites += MenuObject_RemoveSprites;
            On.Menu.MenuScene.Show += MenuScene_Show;
            On.Menu.MenuScene.Hide += MenuScene_Hide;

            editor = new AttachedField<MenuScene, SceneEditor>();
            editor.OnCulled += (key, val) =>
            {
                val.Remove();
            };
        }

        private static void MenuScene_Hide(On.Menu.MenuScene.orig_Hide orig, MenuScene self)
        {
            orig(self);
            editor[self]?.Hide();
        }

        private static void MenuScene_Show(On.Menu.MenuScene.orig_Show orig, MenuScene self)
        {
            orig(self);
            editor[self]?.Show();
        }

        private static void MenuObject_RemoveSprites(On.Menu.MenuObject.orig_RemoveSprites orig, MenuObject self)
        {
            orig(self);
            if (self is MenuScene scene)
                editor[scene]?.Remove();
        }

        private static void MenuScene_GrafUpdate(On.Menu.MenuScene.orig_GrafUpdate orig, MenuScene self, float timeStacker)
        {
            orig(self, timeStacker);

            if (editor.TryGet(self, out SceneEditor se))
            {
                se.Update(self);
                if(Input.GetKeyDown(KeyCode.RightBracket))
                {
                    se.Remove();
                    editor.Unset(self);
                }
            }
            else
            {
                if (self.sceneFolder == resourceFolderName && Input.GetKeyDown(KeyCode.RightBracket))
                {
                    CustomPlayer ply = null;
                    foreach(MenuObject subObj in self.subObjects)
                    {
                        if(subObj is MenuIllustration illust)
                        {
                            ply = customRep[illust]?.Owner.Owner;
                            if (ply != null) break;
                        }
                    }
                    if (ply.DevMode)
                        editor[self] = new SceneEditor(self);
                }
            }
        }

        // INSTANCE //

        private bool alive = true;
        private List<MoveHandle> handles;

        public SceneEditor(MenuScene owner)
        {
            handles = new List<MoveHandle>();
        }

        public void Show()
        {
            foreach (MoveHandle handle in handles)
                handle.Show();
        }

        public void Hide()
        {
            foreach (MoveHandle handle in handles)
                handle.Hide();
        }

        public void Update(MenuScene owner)
        {
            if (!alive) return;
            int handle = 0;

            Vector2? mousePos = Input.mousePosition;

            // Fade out handles not close to the mouse
            MenuIllustration closestIllust = null;
            {
                float minIllustDist = 20f * 20f;
                for (int i = 0; i < owner.subObjects.Count; i++)
                {
                    if (!(owner.subObjects[i] is MenuIllustration illust)) continue;
                    float dist = Vector2.SqrMagnitude(illust.pos + illust.size / 2f - mousePos.Value);
                    if(minIllustDist > dist)
                    {
                        closestIllust = illust;
                        minIllustDist = dist;
                    }
                }
            }

            // Update move handles
            for (int i = 0; i < owner.subObjects.Count; i++)
            {
                if (!(owner.subObjects[i] is MenuIllustration illust)) continue;
                if (!customRep.TryGet(illust, out SceneImage csi)) continue;

                Vector2 centerPos = illust.pos + illust.size / 2f;
                if (handles.Count <= handle)
                {
                    handles.Add(new MoveHandle(csi.DisplayName));
                }
                handles[handle].SetVisible(illust.sprite.concatenatedAlpha > 0f);
                handles[handle].Update(ref centerPos, ref mousePos, closestIllust != null && closestIllust != illust);
                illust.pos = centerPos - illust.size / 2f;
                csi.Pos = illust.pos;
                handle++;
            }

            // Save on request
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                CustomScene sceneToSave = null;
                foreach (var subObj in owner.subObjects)
                {
                    if (!(subObj is MenuIllustration illust)) continue;

                    SceneImage csi = customRep[illust];
                    if (csi != null && csi.Owner.dirty)
                    {
                        sceneToSave = csi.Owner;
                        break;
                    }
                }

                if(sceneToSave != null)
                    SaveEditedScene(sceneToSave);
            }
        }

        private static void SaveEditedScene(CustomScene scene)
        {
            // Write the scene to a file
            try
            {
                string outPath = string.Join(Path.DirectorySeparatorChar.ToString(), new string[] {
                    scene.Owner.DefaultResourcePath,
                    "Scenes",
                    scene.Name,
                    "scene.json"
                });

                // Save all images to a JSON object
                Dictionary<string, object> jsonObj = scene.ToJsonObj();
                foreach (var img in scene.Images)
                    img.OnSave(jsonObj);

                // Write to a file
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, jsonObj.toJson());
                
                // Unset all dirty flags
                foreach(var img in scene.Images)
                    img.dirty = false;
                scene.dirty = false;
            } catch(Exception e)
            {
                Debug.Log("Failed to save scene to file!");
                Debug.LogException(e);
            }
        }

        public void Remove()
        {
            if (!alive) return;
            alive = false;

            foreach(MoveHandle handle in handles)
            {
                handle.Remove();
            }
            handles = null;
        }

        internal class MoveHandle
        {
            private FSprite handle;
            private FLabel name;
            private FLabel nameShadow;
            private bool dragging;
            private bool hidden;

            public MoveHandle(string name)
            {
                handle = new FSprite("buttonCircleA") { anchorX = 0.5f, anchorY = 0.5f, color = Color.red };
                this.name = new FLabel("font", name) { anchorX = 0f, anchorY = 0.5f };
                nameShadow = new FLabel("font", name) { anchorX = 0f, anchorY = 0.5f, color = Color.black };
                Futile.stage.AddChild(handle);
                Futile.stage.AddChild(nameShadow);
                Futile.stage.AddChild(this.name);
            }

            public void Update(ref Vector2 handlePos, ref Vector2? mousePos, bool dark)
            {
                if (hidden)
                {
                    dragging = false;
                    return;
                }
                handle.alpha = dark ? 0.4f : 1f;
                name.alpha = dark ? 0.2f : 1f;
                nameShadow.alpha = dark ? 0.1f : 0.75f;
                if(mousePos.HasValue && Input.GetMouseButton(0))
                {
                    if (dragging)
                    {
                        handlePos = mousePos.Value;
                        mousePos = null;
                    }
                    else
                    {
                        if(PointOverHandle(mousePos.Value, handlePos) && Input.GetMouseButtonDown(0))
                        {
                            dragging = true;
                            mousePos = null;
                        }
                    }
                } else
                {
                    dragging = false;
                }

                Vector2 drawPos = new Vector2(Mathf.Floor(handlePos.x) + 0.1f, Mathf.Floor(handlePos.y) + 0.1f);
                handle.SetPosition(drawPos);
                drawPos.x += 12f;
                name.SetPosition(drawPos);
                drawPos.x += 1f;
                drawPos.y -= 1f;
                nameShadow.SetPosition(drawPos);
            }

            public void SetVisible(bool visible)
            {
                if(visible == hidden)
                {
                    if (visible) Show();
                    else Hide();
                }
            }

            public void Hide()
            {
                hidden = true;
                handle.isVisible = false;
                name.isVisible = false;
                nameShadow.isVisible = false;
            }

            public void Show()
            {
                hidden = false;
                handle.isVisible = true;
                name.isVisible = true;
                nameShadow.isVisible = true;
            }

            public void Remove()
            {
                handle.RemoveFromContainer();
                name.RemoveFromContainer();
                nameShadow.RemoveFromContainer();
            }

            private bool PointOverHandle(Vector2 pos, Vector2 handlePos)
            {
                return Custom.DistLess(pos, handlePos, 8.5f);
            }
        }
    }
}
