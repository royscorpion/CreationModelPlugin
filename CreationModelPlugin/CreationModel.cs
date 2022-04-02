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
    [Transaction(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            #region Исходные данные для построения

            double wallLength = 10000;
            double wallWidth = 5000;
            string wallBaseLevelName = "Уровень 1";
            string wallTopConstraintLevelName = "Уровень 2";

            #endregion

            #region Построение

            //Создание стеновой коробки
            List<Wall> walls = CreateWallBox(doc, wallLength, wallWidth, wallBaseLevelName, wallTopConstraintLevelName, false);

            #endregion

            return Result.Succeeded;
        }


        #region Методы построения

        //Метод построения стеновой коробки (прямоугольник на плане)
        public List<Wall> CreateWallBox(Document doc, double length, double width, string levelName, string topConstraintLevel, bool structural)
        {
            Level level1 = GetLevelByName(doc, levelName);
            Level level2 = GetLevelByName(doc, topConstraintLevel);

            List<XYZ> points = GetRectangleCornersBySizes(length, width);

            List<Wall> walls = new List<Wall>();

            Transaction transaction = new Transaction(doc, "Create walls");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, structural);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }

            transaction.Commit();
            return walls;

        }

        //Метод получения уровня по его наименованию
        public Level GetLevelByName(Document doc, string name)
        {
            List<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            Level level = levels
                .Where(x => x.Name.Equals(name))
                .FirstOrDefault();
            return level;
        }

        ///Метод получения точек углов прямоугольника (относительно центра прямоугольника) по его длинам его сторон, заданных в миллиметрах,
        ///с конвертацией во внутренние единицы измерения
        public List<XYZ> GetRectangleCornersBySizes(double a, double b)
        {
            double dx = UnitUtils.ConvertToInternalUnits(a / 2, UnitTypeId.Millimeters);
            double dy = UnitUtils.ConvertToInternalUnits(b / 2, UnitTypeId.Millimeters);

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            return points;
        }

        #endregion
    }
}
