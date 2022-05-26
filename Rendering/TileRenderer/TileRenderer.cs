using Mapster.Common.MemoryMappedTypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mapster.Rendering;

public static class TileRenderer
{
    public static BaseShape Tessellate(this MapFeatureData feature, ref BoundingBox boundingBox,
        ref PriorityQueue<BaseShape, int> shapes)
    {
        BaseShape? baseShape = null;
        var featureType = feature.Type;

        var isPopulatedPlace = false;
        var isBorder = false;
        
        foreach (var p in feature.Properties)
        {
            if (p is >= (ushort) PropEnum.HMOTORWAY and <= (ushort) PropEnum.HROAD)
            {
                var coordinates = feature.Coordinates;
                var road = new Road(coordinates);
                baseShape = road;
                shapes.Enqueue(road, road.ZIndex);
            }
            else if (p.Equals((ushort) PropEnum.WATER) && feature.Type != GeometryType.Point)
            {
                var coordinates = feature.Coordinates;

                var waterway = new Waterway(coordinates, feature.Type == GeometryType.Polygon);
                baseShape = waterway;
                shapes.Enqueue(waterway, waterway.ZIndex);
            }
            else if (!isBorder && Border.ShouldBeBorder(feature))
            {
                var coordinates = feature.Coordinates;
                var border = new Border(coordinates);
                baseShape = border;
                shapes.Enqueue(border, border.ZIndex);

                isBorder = true;
            }
            else if (!isPopulatedPlace && PopulatedPlace.ShouldBePopulatedPlace(feature, p))
            {
                var coordinates = feature.Coordinates;
                var popPlace = new PopulatedPlace(coordinates, feature);
                baseShape = popPlace;
                shapes.Enqueue(popPlace, popPlace.ZIndex);

                isPopulatedPlace = true;
            }

            else if (p.Equals((ushort) PropEnum.RAILWAY))
            {
                var coordinates = feature.Coordinates;
                var railway = new Railway(coordinates);
                baseShape = railway;
                shapes.Enqueue(railway, railway.ZIndex);
            }
            else if (p is >= (ushort) PropEnum.NFELL and <= (ushort) PropEnum.NWATER &&
                     featureType == GeometryType.Polygon)
            {
                var coordinates = feature.Coordinates;
                var geoFeature = new GeoFeature(coordinates, feature, p);
                baseShape = geoFeature;
                shapes.Enqueue(geoFeature, geoFeature.ZIndex);
            }
            else if (p.Equals((ushort) PropEnum.BFOREST))
            {
                var coordinates = feature.Coordinates;
                var geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Forest);
                baseShape = geoFeature;
                shapes.Enqueue(geoFeature, geoFeature.ZIndex);
            }
            else if (p.Equals((ushort) PropEnum.LFOREST) || p.Equals((ushort) PropEnum.LORCHARD))
            {
                var coordinates = feature.Coordinates;
                var geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Forest);
                baseShape = geoFeature;
                shapes.Enqueue(geoFeature, geoFeature.ZIndex);
            }
            else if (feature.Type == GeometryType.Polygon &&
                     p is >= (ushort) PropEnum.LRESIDENTIAL and <= (ushort) PropEnum.LBROWNFIELD)
            {
                var coordinates = feature.Coordinates;
                var geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Residential);
                baseShape = geoFeature;
                shapes.Enqueue(geoFeature, geoFeature.ZIndex);
            }
            else if (feature.Type == GeometryType.Polygon &&
                     p is >= (ushort) PropEnum.LFARM and <= (ushort) PropEnum.LALOTTMENTS)
            {
                var coordinates = feature.Coordinates;
                var geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Plain);
                baseShape = geoFeature;
                shapes.Enqueue(geoFeature, geoFeature.ZIndex);
            }
            else if (feature.Type == GeometryType.Polygon && p.Equals((ushort) PropEnum.LRESERVOIR) ||
                     p.Equals((ushort) PropEnum.LBASIN))
            {
                var coordinates = feature.Coordinates;
                var geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Water);
                baseShape = geoFeature;
                shapes.Enqueue(geoFeature, geoFeature.ZIndex);
            }
            else if (feature.Type == GeometryType.Polygon && p.Equals((ushort) PropEnum.BUILDING))
            {
                var coordinates = feature.Coordinates;
                var geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Residential);
                baseShape = geoFeature;
                shapes.Enqueue(geoFeature, geoFeature.ZIndex);
            }
            else if (feature.Type == GeometryType.Polygon && p.Equals((ushort) PropEnum.LEISURE))
            {
                var coordinates = feature.Coordinates;
                var geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Residential);
                baseShape = geoFeature;
                shapes.Enqueue(geoFeature, geoFeature.ZIndex);
            }
            else if (feature.Type == GeometryType.Polygon && p.Equals((ushort) PropEnum.AMENITY))
            {
                var coordinates = feature.Coordinates;
                var geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Residential);
                baseShape = geoFeature;
                shapes.Enqueue(geoFeature, geoFeature.ZIndex);
            }
        }

        if (baseShape != null)
        {
            for (var j = 0; j < baseShape.ScreenCoordinates.Length; ++j)
            {
                boundingBox.MinX = Math.Min(boundingBox.MinX, baseShape.ScreenCoordinates[j].X);
                boundingBox.MaxX = Math.Max(boundingBox.MaxX, baseShape.ScreenCoordinates[j].X);
                boundingBox.MinY = Math.Min(boundingBox.MinY, baseShape.ScreenCoordinates[j].Y);
                boundingBox.MaxY = Math.Max(boundingBox.MaxY, baseShape.ScreenCoordinates[j].Y);
            }
        }

        return baseShape;
    }

    public static Image<Rgba32> Render(this PriorityQueue<BaseShape, int> shapes, BoundingBox boundingBox, int width,
        int height)
    {
        var canvas = new Image<Rgba32>(width, height);

        // Calculate the scale for each pixel, essentially applying a normalization
        var scaleX = canvas.Width / (boundingBox.MaxX - boundingBox.MinX);
        var scaleY = canvas.Height / (boundingBox.MaxY - boundingBox.MinY);
        var scale = Math.Min(scaleX, scaleY);

        // Background Fill
        canvas.Mutate(x => x.Fill(Color.White));
        while (shapes.Count > 0)
        {
            var entry = shapes.Dequeue();
            // FIXME: Hack
            if (entry.ScreenCoordinates.Length < 2)
            {
                continue;
            }

            entry.TranslateAndScale(boundingBox.MinX, boundingBox.MinY, scale, canvas.Height);
            canvas.Mutate(x => entry.Render(x));
        }

        return canvas;
    }

    public struct BoundingBox
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;
    }
}