using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [Transaction(TransactionMode.Manual)] //атрибут, указывающий что будет делать данное приложение - считывать данные (ReadOnly) или изменять их (Manual)
    public class Lab4 : IExternalCommand  //переименование класса Class1 в Main (Ctrl+RR). Чтобы реализовать приложение для Revit необходимо реализовать интерфейс IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            //System Family - Instance
            var res1 =  new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                //.Cast<Wall>()
                .OfType<Wall>()
                .ToList();

            //System Family - Family Type
            var res2 =  new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                //.Cast<Wall>()
                .OfType<WallType>()
                .ToList();

            //Component Family - FamilyInstance
            var res3 = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Doors)
                //.Cast<Wall>()
                .OfType<FamilyInstance>()
                .Where(x=>x.Name.Equals("0915 x 2134 мм"))
                .ToList();

            //Component Family - FamilySymbol
            var res32 = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToList();

            return Result.Succeeded;
        }
    }
}
