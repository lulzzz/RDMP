// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BrightIdeasSoftware;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Providers;
using Rdmp.Core.Repositories;
using Rdmp.Core.Repositories.Construction;
using Rdmp.UI.Collections.Providers;
using Rdmp.UI.Collections.Providers.Copying;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.CommandExecution.AtomicCommands.UIFactory;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using Rdmp.UI.Menus;
using Rdmp.UI.Menus.MenuItems;
using Rdmp.UI.Refreshing;
using Rdmp.UI.Theme;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;
using ReusableLibraryCode.Settings;
using ReusableUIComponents.TreeHelper;

namespace Rdmp.UI.Collections
{
    /// <summary>
    /// Provides centralised functionality for all RDMPCollectionUI classes.  This includes configuring TreeListView to use the correct icons, have the correct row 
    /// height, child nodes etc.  Also centralises functionality like applying a CollectionPinFilterUI to an RDMPCollectionUI, keeping trees up to date during object
    /// refreshes / deletes etc.
    /// </summary>
    public class RDMPCollectionCommonFunctionality : IRefreshBusSubscriber
    {
        private RDMPCollection _collection;

        private IActivateItems _activator;
        public TreeListView Tree;

        public ICoreIconProvider CoreIconProvider { get; private set; }
        public ICoreChildProvider CoreChildProvider { get; set; }
        public RenameProvider RenameProvider { get; private set; }
        public DragDropProvider DragDropProvider { get; private set; }
        public CopyPasteProvider CopyPasteProvider { get; private set; }
        public FavouriteColumnProvider FavouriteColumnProvider { get; private set; }
        public TreeNodeParentFinder ParentFinder { get; private set; }
        
        public IRDMPPlatformRepositoryServiceLocator RepositoryLocator { get; private set; }
        
        public OLVColumn FavouriteColumn { get; private set; }

        public bool IsSetup { get; private set; }
        
        public Func<IActivateItems,IAtomicCommand[]> WhitespaceRightClickMenuCommandsGetter { get; set; }
        
        private CollectionPinFilterUI _pinFilter;
        public object CurrentlyPinned { get; private set; }

        public IDColumnProvider IDColumnProvider { get; set; }
        public OLVColumn IDColumn { get; set; }
        public CheckColumnProvider CheckColumnProvider { get; set; }
        public OLVColumn CheckColumn { get; set; }

        /// <summary>
        /// List of Types for which the children should not be returned.  By default the IActivateItems child provider knows all objects children all the way down
        /// You can cut off any branch with this property, just specify the Types to stop descending at and you will get that object Type (assuming you normally would)
        /// but no further children.
        /// </summary>
        public Type[] AxeChildren { get; set; }

        public Type[] MaintainRootObjects { get; set; }
        
        public RDMPCollectionCommonFunctionalitySettings Settings { get; private set; }
         
        private static readonly Dictionary<RDMPCollection,Guid> TreeGuids = new Dictionary<RDMPCollection, Guid>()
        {
            {RDMPCollection.Tables,new Guid("8f24d624-acad-45dd-862b-01b18dfdd9a2")},
            {RDMPCollection.Catalogue,new Guid("d0f72b03-63f1-487e-9afa-51c03afa7819")},
            {RDMPCollection.DataExport,new Guid("9fb651f6-3e4f-4629-b64e-f61551ae009e")},
            {RDMPCollection.SavedCohorts,new Guid("6d0e4560-9357-4ee1-91b6-a182a57f7a6f")},
            {RDMPCollection.Cohort,new Guid("5c7cceb3-4202-47b1-b271-e2eed869d9ef")},
            {RDMPCollection.Favourites,new Guid("39d37439-ac7a-4346-8c79-9867384db92e")},
            {RDMPCollection.DataLoad,new Guid("600aad33-df6c-4013-ad92-65de19d494cf")},
        };

        /// <summary>
        /// Sets up common functionality for an RDMPCollectionUI with the default settings
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="tree">The main tree in the collection UI</param>
        /// <param name="activator">The current activator, used to launch objects, register for refresh events etc </param>
        /// <param name="iconColumn">The column of tree view which should contain the icon for each row object</param>
        /// <param name="renameableColumn">Nullable field for specifying which column supports renaming on F2</param>
        public void SetUp(RDMPCollection collection, TreeListView tree, IActivateItems activator, OLVColumn iconColumn, OLVColumn renameableColumn)
        {
            SetUp(collection,tree,activator,iconColumn,renameableColumn,new RDMPCollectionCommonFunctionalitySettings());
        }

        /// <summary>
        /// Sets up common functionality for an RDMPCollectionUI
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="tree">The main tree in the collection UI</param>
        /// <param name="activator">The current activator, used to launch objects, register for refresh events etc </param>
        /// <param name="iconColumn">The column of tree view which should contain the icon for each row object</param>
        /// <param name="renameableColumn">Nullable field for specifying which column supports renaming on F2</param>
        /// <param name="settings">Customise which common behaviorurs are turned on</param>
        public void SetUp(RDMPCollection collection, TreeListView tree, IActivateItems activator, OLVColumn iconColumn, OLVColumn renameableColumn,RDMPCollectionCommonFunctionalitySettings settings)
        {
            Settings = settings;
            _collection = collection;
            IsSetup = true;
            _activator = activator;
            _activator.RefreshBus.Subscribe(this);

            RepositoryLocator = _activator.RepositoryLocator;

            Tree = tree;
            Tree.FullRowSelect = true;
            Tree.HideSelection = false;
            Tree.KeyPress += Tree_KeyPress;

            Tree.RevealAfterExpand = true;

            if (!Settings.SuppressChildrenAdder)
            {
                Tree.CanExpandGetter += CanExpandGetter;
                Tree.ChildrenGetter += ChildrenGetter;
            }

            if(!Settings.SuppressActivate)
                Tree.ItemActivate += CommonItemActivation;

            Tree.CellRightClick += CommonRightClick;
            Tree.SelectionChanged += (s,e)=>RefreshContextMenuStrip();
            
            if(iconColumn != null)
                iconColumn.ImageGetter += ImageGetter;
            
            if(Tree.RowHeight != 19)
                Tree.RowHeight = 19;

            //add colour indicator bar
            Tree.Location = new Point(Tree.Location.X, tree.Location.Y+3);
            Tree.Height -= 3;

            CreateColorIndicator(Tree,collection);

            //what does this do to performance?
            Tree.UseNotifyPropertyChanged = true;

            ParentFinder = new TreeNodeParentFinder(Tree);

            DragDropProvider = new DragDropProvider(
                _activator.CommandFactory,
                _activator.CommandExecutionFactory,
                tree);

            if(renameableColumn != null)
            {
                RenameProvider = new RenameProvider(_activator, tree, renameableColumn);
                RenameProvider.RegisterEvents();
            }

            if (Settings.AddFavouriteColumn)
            {
                FavouriteColumnProvider = new FavouriteColumnProvider(_activator, tree);
                FavouriteColumn = FavouriteColumnProvider.CreateColumn();
            }

            if (settings.AddIDColumn)
            {
                IDColumnProvider = new IDColumnProvider(tree);
                IDColumn = IDColumnProvider.CreateColumn();

                Tree.AllColumns.Add(IDColumn);
                Tree.RebuildColumns();
            }

            if (Settings.AddCheckColumn)
            {
                CheckColumnProvider = new CheckColumnProvider(tree, _activator.CoreIconProvider);
                CheckColumn = CheckColumnProvider.CreateColumn();
                
                Tree.AllColumns.Add(CheckColumn);
                Tree.RebuildColumns();
            }
            CoreIconProvider = activator.CoreIconProvider;

            CopyPasteProvider = new CopyPasteProvider();
            CopyPasteProvider.RegisterEvents(tree);
            
            OnRefreshChildProvider(_activator.CoreChildProvider);
            
            _activator.Emphasise += _activator_Emphasise;

            Tree.TreeFactory = TreeFactoryGetter;
            Tree.RebuildAll(true);
            
            Tree.FormatRow += Tree_FormatRow;
            Tree.CellToolTipGetter += Tree_CellToolTipGetter;

            if(Settings.AllowSorting)
            {
                if (Tree.PrimarySortColumn == null)
                    Tree.PrimarySortColumn = Tree.AllColumns.FirstOrDefault(c => c.IsVisible && c.Sortable);

                //persist user sort orders
                if (TreeGuids.ContainsKey(_collection))
                {
                    //if we know the sort order fo this collection last time
                    var lastSort = UserSettings.GetLastColumnSortForCollection(TreeGuids[_collection]);

                    //reestablish that sort order
                    if (lastSort != null && Tree.AllColumns.Any(c => c.Text == lastSort.Item1))
                    {
                        Tree.PrimarySortColumn = Tree.GetColumn(lastSort.Item1);
                        Tree.PrimarySortOrder = lastSort.Item2 ? SortOrder.Ascending : SortOrder.Descending;
                    }

                    //and track changes to the sort order
                    Tree.AfterSorting += TreeOnAfterSorting;
                }
            }
            else
                foreach (OLVColumn c in Tree.AllColumns)
                    c.Sortable = false;
        }

        void Tree_KeyPress(object sender, KeyPressEventArgs e)
        {
            //Prevents keyboard 'bong' sound occuring when using Enter to activate an object
            if (e.KeyChar == (char)Keys.Enter)
                e.Handled = true;
        }

        private void TreeOnAfterSorting(object sender, AfterSortingEventArgs e)
        {
            if (TreeGuids.ContainsKey(_collection))
                UserSettings.SetLastColumnSortForCollection(TreeGuids[_collection], e.ColumnToSort == null ? null:e.ColumnToSort.Text, e.SortOrder == SortOrder.Ascending);
        }

        private void CreateColorIndicator(TreeListView tree, RDMPCollection collection)
        {
            if(Tree.Parent == null || collection == RDMPCollection.None)
                return;

            var indicatorHeight = BackColorProvider.IndiciatorBarSuggestedHeight;

            BackColorProvider p = new BackColorProvider();
            var ctrl = new Control();
            ctrl.BackColor = p.GetColor(collection);
            ctrl.Location = new Point(Tree.Location.X, tree.Location.Y - indicatorHeight);
            ctrl.Height = indicatorHeight;
            ctrl.Width = Tree.Width;

            if (Tree.Dock != DockStyle.None)
                ctrl.Dock = DockStyle.Top;
            else
                ctrl.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

            Tree.Parent.Controls.Add(ctrl);
        }

        private string Tree_CellToolTipGetter(OLVColumn column, object modelObject)
        {
            return  _activator.DescribeProblemIfAny(modelObject);
        }

        void Tree_FormatRow(object sender, FormatRowEventArgs e)
        {
            bool hasProblems = _activator.HasProblem(e.Model);

            var disableable = e.Model as IDisableable;

            if (disableable != null && disableable.IsDisabled)
            {
                e.Item.ForeColor = Color.FromArgb(152,152,152);
                
                //make it italic
                if(!e.Item.Font.Italic)
                    e.Item.Font = new Font(e.Item.Font,FontStyle.Italic);

                e.Item.BackColor = Color.FromArgb(225,225,225);
            }
            else
            {
                //make it not italic
                if(e.Item.Font.Italic)
                    e.Item.Font = new Font(e.Item.Font,FontStyle.Regular);

                e.Item.ForeColor = hasProblems ? Color.Red : Color.Black;
                e.Item.BackColor = hasProblems ? Color.FromArgb(255,220,220) : Color.White;
            }
        }

        private TreeListView.Tree TreeFactoryGetter(TreeListView view)
        {
            return new RDMPCollectionCommonFunctionalityTreeHijacker(view);
        }
        
        private void RefreshContextMenuStrip()
        {
            if(Tree.SelectedObjects.Count <= 1)
                Tree.ContextMenuStrip = GetMenuIfExists(Tree.SelectedObject);
            else
                Tree.ContextMenuStrip = GetMenuIfExists(Tree.SelectedObjects);
        }

        public void CommonRightClick(object sender, CellRightClickEventArgs e)
        {
            //if we aren't doing a multi select
            if(Tree.SelectedObjects.Count <= 1)
            {
                Tree.SelectedObject = e.Model;
                RefreshContextMenuStrip();
            }
        }

        void _activator_Emphasise(object sender, ItemActivation.Emphasis.EmphasiseEventArgs args)
        {
            var rootObject = _activator.GetRootObjectOrSelf(args.Request.ObjectToEmphasise);

            // unpin first if there is somthing pinned, so we find our object!
            if (_pinFilter != null && _activator.IsRootObjectOfCollection(_collection,rootObject))
                _pinFilter.UnApplyToTree();
            
            //get the parental hierarchy
            var decendancyList = CoreChildProvider.GetDescendancyListIfAnyFor(args.Request.ObjectToEmphasise);
            
            if (decendancyList != null)
            {
                //for each parent in the decendandy list
                foreach (var parent in decendancyList.Parents)
                {
                    //parent isn't in our tree
                    if (Tree.IndexOf(parent) == -1)
                        return;

                    //parent is in our tree so make sure it's expanded
                    Tree.Expand(parent);
                }
            }

            //tree doesn't contain object even after expanding parents
            int index = Tree.IndexOf(args.Request.ObjectToEmphasise);

            if(index == -1)
                return;

            if (args.Request.ExpansionDepth > 0)
                try
                {
                    Tree.BeginUpdate();
                    ExpandToDepth(args.Request.ExpansionDepth, args.Request.ObjectToEmphasise);
                }
                finally
                {
                    Tree.EndUpdate();
                }
                
            if (args.Request.Pin && Settings.AllowPinning)
                Pin(args.Request.ObjectToEmphasise, decendancyList);

            //update index now pin filter is applied
            index = Tree.IndexOf(args.Request.ObjectToEmphasise);

            //select the object and ensure it's visible
            Tree.SelectedObject = args.Request.ObjectToEmphasise;
            Tree.EnsureVisible(index);

            
            args.FormRequestingActivation = Tree.FindForm();
        }

        private void Pin(IMapsDirectlyToDatabaseTable objectToPin, DescendancyList descendancy)
        {
            if (_pinFilter != null)
                _pinFilter.UnApplyToTree();
            
            _pinFilter = new CollectionPinFilterUI();
            _pinFilter.ApplyToTree(_activator, Tree, objectToPin, descendancy);
            CurrentlyPinned = objectToPin;

            _pinFilter.UnApplied += (s, e) =>
            {
                _pinFilter = null;
                CurrentlyPinned = null;
            };
        }

        /// <summary>
        /// Expands the current object (which must exist/be visible in the UI) to the given depth
        /// </summary>
        /// <param name="expansionDepth"></param>
        /// <param name="currentObject"></param>
        public void ExpandToDepth(int expansionDepth, object currentObject)
        {
            if(expansionDepth == 0)
                return;

            Tree.Expand(currentObject);

            foreach (object o in ChildrenGetter(currentObject))
                ExpandToDepth(expansionDepth -1,o);
        }

        private IEnumerable ChildrenGetter(object model)
        {
            if (AxeChildren != null && AxeChildren.Contains(model.GetType()))
                return new object[0];

            return CoreChildProvider.GetChildren(model);
        }

        private bool CanExpandGetter(object model)
        {
            var result = ChildrenGetter(model);

            if (result == null)
                return false;
            
            return result.Cast<object>().Any();
        }

        private object ImageGetter(object rowObject)
        {
            bool hasProblems = _activator.HasProblem(rowObject);
            
            return CoreIconProvider.GetImage(rowObject,hasProblems?OverlayKind.Problem:OverlayKind.None);
        }

        /// <summary>
        /// Creates a menu compatible with object <paramref name="o"/>.  Returns null if no compatible menu exists.
        /// Errors are reported to <see cref="IActivateItems.GlobalErrorCheckNotifier"/> (if set up).
        /// 
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public ContextMenuStrip GetMenuIfExists(object o)
        {
            try
            {
                var many = o as ICollection;

                if (many != null)
                {
                    var menu = new ContextMenuStrip();

                    var factory = new AtomicCommandUIFactory(_activator);

                    if (many.Cast<object>().All(d => d is IDeleteable))
                    {
                        var mi = factory.CreateMenuItem(new ExecuteCommandDelete(_activator, many.Cast<IDeleteable>().ToList()));
                        mi.ShortcutKeys = Keys.Delete;
                        menu.Items.Add(mi);
                    }

                    return menu;
                }

                if (o != null)
                {
                    //is o masquerading as someone else?
                    IMasqueradeAs masquerader = o as IMasqueradeAs;

                    //if so this is who he is pretending to be
                    object masqueradingAs = null;

                    if (masquerader != null)
                        masqueradingAs = masquerader.MasqueradingAs(); //yes he is masquerading!

                    var menu = GetMenuWithCompatibleConstructorIfExists(o);

                    //If no menu takes the object o try checking the object it is masquerading as as a secondary preference
                    if (menu == null && masqueradingAs != null)
                        menu = GetMenuWithCompatibleConstructorIfExists(masqueradingAs, masquerader);

                    //found a menu with compatible constructor arguments
                    if (menu != null)
                    {
                        if (!Settings.AllowPinning)
                        {
                            var miPin = menu.Items.OfType<AtomicCommandMenuItem>().SingleOrDefault(mi => mi.Tag is ExecuteCommandPin);

                            if (miPin != null)
                            {
                                miPin.Enabled = false;
                                miPin.ToolTipText = "Pinning is disabled in this collection";
                            }
                        }

                        return menu;
                    }

                    //no compatible menus so just return default menu
                    var defaultMenu = new RDMPContextMenuStrip(new RDMPContextMenuStripArgs(_activator, Tree, o), o);
                    defaultMenu.AddCommonMenuItems(this);
                    return defaultMenu;
                }
                else
                {
                    //it's a right click in whitespace (nothing right clicked)

                    AtomicCommandUIFactory factory = new AtomicCommandUIFactory(_activator);

                    if (WhitespaceRightClickMenuCommandsGetter != null)
                    {
                        var menu = factory.CreateMenu(_activator, Tree, _collection, WhitespaceRightClickMenuCommandsGetter(_activator));
                        menu.AddCommonMenuItems(this);
                        return menu;

                    }
                }

                return null;
            }
            catch(Exception ex)
            {
                if(_activator?.GlobalErrorCheckNotifier == null)
                    throw;

                _activator.GlobalErrorCheckNotifier.OnCheckPerformed(new CheckEventArgs($"Failed to build menu for {o} of Type {o?.GetType()}",CheckResult.Fail,ex));
                return null;
            }           
        }

        //once we find the best menu for object of Type x then we want to cache that knowledge and go directly to that menu every time
        Dictionary<Type,Type> _cachedMenuCompatibility = new Dictionary<Type, Type>();
        
        private ContextMenuStrip GetMenuWithCompatibleConstructorIfExists(object o, IMasqueradeAs oMasquerader = null)
        {
            RDMPContextMenuStripArgs args = new RDMPContextMenuStripArgs(_activator,Tree,o);
            args.CurrentlyPinnedObject = CurrentlyPinned;
            args.Masquerader = oMasquerader ?? o as IMasqueradeAs;

            var objectConstructor = new ObjectConstructor();

            Type oType = o.GetType();

            //if we have encountered this object type before
            if (_cachedMenuCompatibility.ContainsKey(oType))
            {
                Type compatibleMenu = _cachedMenuCompatibility[oType];
                
                //we know there are no menus compatible with o
                if (compatibleMenu == null)
                    return null;

                return ConstructMenu(objectConstructor, _cachedMenuCompatibility[oType], args, o);
            }
                

            //now find the first RDMPContextMenuStrip with a compatible constructor
            foreach (Type menuType in _activator.RepositoryLocator.CatalogueRepository.MEF.GetTypes<RDMPContextMenuStrip>())
            {
                if (menuType.IsAbstract || menuType.IsInterface || menuType == typeof(RDMPContextMenuStrip))
                    continue;

                //try constructing menu with:
                var menu = ConstructMenu(objectConstructor,menuType,args,o);

                //find first menu that's compatible
                if (menu != null)
                {
                    if (!_cachedMenuCompatibility.ContainsKey(oType))
                        _cachedMenuCompatibility.Add(oType, menu.GetType());

                    return menu;
                }
            }

            //we know there are no menus compatible with this type
            if (!_cachedMenuCompatibility.ContainsKey(oType))
                _cachedMenuCompatibility.Add(oType, null);

            //there are no derrived classes with compatible constructors
            return null;
        }

        private RDMPContextMenuStrip ConstructMenu(ObjectConstructor objectConstructor, Type type, RDMPContextMenuStripArgs args, object o)
        {
            //there is a compatible menu Type known
            
            //parameter 1 must be args
            //parameter 2 must be object compatible Type

            var menu = (RDMPContextMenuStrip)objectConstructor.ConstructIfPossible(type, args, o);
            
            if(menu != null)
                menu.AddCommonMenuItems(this);

            return menu;
        }


        public void CommonItemActivation(object sender, EventArgs eventArgs)
        {
            var o = Tree.SelectedObject;
            
            if(o == null)
                return;

            var cmd = new ExecuteCommandActivate(_activator, o);
            if(!cmd.IsImpossible)
                cmd.Execute();
        }

        public void RefreshBus_RefreshObject(object sender, RefreshObjectEventArgs e)
        {
            OnRefreshChildProvider(_activator.CoreChildProvider);

            RefreshObject(e.Object,e.Exists);

            //now tell tree view to refresh the object
            
            //if the descendancy is known 
            if (_pinFilter != null)
                _pinFilter.OnRefreshObject(_activator.CoreChildProvider,e);


            RefreshContextMenuStrip();

            //also refresh anyone who is masquerading as e.Object
            foreach (IMasqueradeAs masquerader in _activator.CoreChildProvider.GetMasqueradersOf(e.Object))
                RefreshObject(masquerader, e.Exists);
            
        }

        private void RefreshObject(object o, bool exists)
        {

            //or from known descendancy
            var knownDescendancy = _activator.CoreChildProvider.GetDescendancyListIfAnyFor(o);

            //if it is a root object maintained by this tree and it exists
            if (MaintainRootObjects != null && MaintainRootObjects.Contains(o.GetType()) && exists)
                //if tree doesn't yet contain the object
                if (!Tree.Objects.Cast<object>().Contains(o))
                {
                    Tree.AddObject(o); //add it
                    return;
                }

            if(ShouldClearPinFilterOnRefresh(o,exists))
                _pinFilter.UnApplyToTree();

            if (!exists)
            {
                //clear the current selection (if the object to be deleted is selected)
                if(Tree.IsSelected(o))
                {
                    Tree.SelectedObject = null;
                    Tree.SelectedObjects = null;
                }                   

                //remove it from tree
                Tree.RemoveObject(o);
            }
                

            if(!IsHiddenByFilter(o))
                //By preference refresh the parent that way we deal with hierarchy changes
                if (knownDescendancy != null)
                {
                    var lastParent = knownDescendancy.Parents.LastOrDefault(p => Tree.IndexOf(p) != -1);

                    //does tree have parent?
                    if (lastParent != null)
                        Tree.RefreshObject(lastParent); //refresh parent
                    else
                        //Tree has object but not parent, bad times, maybe BetterRouteExists? Refresh the object if it exists
                       if(exists)
                            Tree.RefreshObject(o);
                }
                else
                //if we have the object
                    if (Tree.IndexOf(o) != -1 && exists)
                        Tree.RefreshObject(o); //it exists so refresh it!
        }

        private bool ShouldClearPinFilterOnRefresh(object o, bool exists)
        {
            //there is no current pin
            if(_pinFilter == null)
                return false;

            //the current pin is the object being deleted
            if(!exists && Equals(CurrentlyPinned, o))
                return true;

            //the current pin does not exist anymore (e.g. if you pinned something low down and deleted something above it)
            if (CurrentlyPinned is DatabaseEntity e && !e.Exists())
                return true;

            return false;
        }

        private bool IsHiddenByFilter(object o)
        {
            return Tree.IsFiltering && !Tree.FilteredObjects.Cast<object>().Contains(o);
        }

        private void OnRefreshChildProvider(ICoreChildProvider coreChildProvider)
        {
            CoreChildProvider = coreChildProvider;
        }

        public void TearDown()
        {
            if(IsSetup)
            {
                _activator.RefreshBus.Unsubscribe(this);
                _activator.Emphasise -= _activator_Emphasise;
            }
        }

    }
}
