﻿using CNTK;
using Emgu.CV.Structure;
using SiaNet.Processing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiaNet.Model
{
    internal class ImageMapInfo
    {
        internal string Filepath;
        internal int Label;
        internal int RotationAngle;
        internal Emgu.CV.CvEnum.FlipType Flip = Emgu.CV.CvEnum.FlipType.None;
        internal int Resize = 0;
    }

    public class ImageDataFrame
    {
        private int[] features;
        private int labels;
        private string folder;
        private bool fromFolder;
        List<ImageMapInfo> folderMapData;

        public ImageDataFrame(Variable feature, Variable label)
        {
            features = feature.Shape.Dimensions.ToArray();
            labels = label.Shape.Dimensions[0];
            Frame = new List<ImageData>();
            counter = 0;
        }

        public ImageDataFrame(string folder, int resize = 0, int numberOfRandomRotation = 0, bool horizontalFlip = false, bool verticalFlip = false)
        {
            this.folder = folder;
            fromFolder = true;
            folderMapData = new List<ImageMapInfo>();
            DirectoryInfo dir = new DirectoryInfo(folder);
            var subfolders = dir.GetDirectories();
            int counter = 1;
            foreach (var item in subfolders)
            {
                var files = item.GetFiles().Select(x => (x.FullName)).ToList();
                foreach (var file in files)
                {
                    folderMapData.Add(new ImageMapInfo() { Filepath = file, Label = counter, RotationAngle = 0, Resize = resize });
                    if (numberOfRandomRotation > 0)
                    {
                        for (int i = 0; i < numberOfRandomRotation; i++)
                        {
                            folderMapData.Add(new ImageMapInfo() { Filepath = file, Label = counter, RotationAngle = new Random(30).Next(10, 360), Resize = resize });
                        }
                    }

                    if (horizontalFlip)
                    {
                        folderMapData.Add(new ImageMapInfo() { Filepath = file, Label = counter, RotationAngle = 0, Flip = Emgu.CV.CvEnum.FlipType.Horizontal, Resize = resize });
                    }

                    if (verticalFlip)
                    {
                        folderMapData.Add(new ImageMapInfo() { Filepath = file, Label = counter, RotationAngle = 0, Flip = Emgu.CV.CvEnum.FlipType.Vertical, Resize = resize });
                    }
                }

                counter++;
            }

            Shuffle();
        }

        private int counter;

        public List<ImageData> Frame { get; set; }

        internal Value CurrentX { get; set; }

        internal Value CurrentY { get; set; }

        internal bool Next(int batchSize)
        {
            bool result = true;
            if(fromFolder)
            {
                result = GetNextFromFolder(batchSize);
            }
            else
            {
                result = GetNextFromFrame(batchSize);
            }

            return true;
        }

        private bool GetNextFromFrame(int batchSize)
        {
            var batchData = Frame.Skip(counter * batchSize).Take(batchSize).ToList();
            if (batchData.Count == 0)
                return false;

            List<byte> byteData = new List<byte>();
            List<byte> labelData = new List<byte>();

            foreach (var cur in batchData)
            {
                foreach (var item in cur.Pixels)
                {
                    foreach (var i in item)
                    {
                        byteData.AddRange(i);
                    }
                }

                for (int i = 1; i <= labels; i++)
                {
                    if (cur.label == i)
                    {
                        labelData.Add(1);
                    }
                    else
                    {
                        labelData.Add(0);
                    }
                }
            }
             
            CurrentX = Value.CreateBatch(features, byteData.Select(b => (float)b).ToList(), GlobalParameters.Device);
            CurrentY = Value.CreateBatch(features, labelData.Select(b => (float)b).ToList(), GlobalParameters.Device);

            return true;
        }

        public bool GetNextFromFolder(int batchSize)
        {
            var batchData = folderMapData.Skip(counter * batchSize).Take(batchSize).ToList();
            if (batchData.Count == 0)
                return false;

            List<float> byteData = new List<float>();
            List<byte> labelData = new List<byte>();

            foreach (var item in batchData)
            {
                byteData.AddRange(processImageFile(item));

                for (int i = 1; i <= labels; i++)
                {
                    if (item.Label == i)
                    {
                        labelData.Add(1);
                    }
                    else
                    {
                        labelData.Add(0);
                    }
                }
            }

            CurrentX = Value.CreateBatch(features, byteData, GlobalParameters.Device);
            CurrentY = Value.CreateBatch(features, labelData.Select(b => (float)b).ToList(), GlobalParameters.Device);

            return true;
        }

        private List<float> processImageFile(ImageMapInfo mapInfo)
        {
            Bitmap bmp = new Bitmap(mapInfo.Filepath);
            Emgu.CV.Image<Bgr, byte> img = new Emgu.CV.Image<Bgr, byte>(bmp);
            if (mapInfo.Resize > 0)
            {
                img = img.Resize(mapInfo.Resize, mapInfo.Resize, Emgu.CV.CvEnum.Inter.Nearest);
            }

            if (mapInfo.Flip != Emgu.CV.CvEnum.FlipType.None)
            {
                img = img.Flip(mapInfo.Flip);
            }

            if (mapInfo.RotationAngle > 0)
            {
                img.Rotate(mapInfo.RotationAngle, new Bgr(Color.White));
            }

            return img.Bitmap.ParallelExtractCHW();
        }

        internal void Reset()
        {
            counter = 1;
            CurrentX = null;
            CurrentY = null;
        }

        private void Shuffle()
        {
            List<ImageMapInfo> clone = folderMapData;
            if (folderMapData.Count > 0)
            {
                clone.Clear();
                Random random = new Random();

                while (folderMapData.Count > 0)
                {
                    int row = random.Next(0, folderMapData.Count);
                    var element = folderMapData.ElementAt(row);
                    clone.Add(element);
                    folderMapData.Remove(element);
                }
            }

            folderMapData = clone;
        }

        private void ExtractCifar10()
        {
            string filepath = @"C:\Users\batt0153\AppData\Local\Downloads\cifar-10-binary\cifar-10-batches-bin\data_batch_1.bin";
            FileStream imageStream = new FileStream(filepath, FileMode.Open);
            BinaryReader br = new BinaryReader(imageStream);
            int pixelSize = 32;

            byte[][][] pixels = new byte[3][][];
            pixels[0] = new byte[pixelSize][];
            pixels[1] = new byte[pixelSize][];
            pixels[2] = new byte[pixelSize][];

            // each test image
            for (int di = 0; di < 10000; ++di)
            {
                byte lbl = br.ReadByte();

                for (int i = 0; i < pixelSize; ++i)
                    pixels[0][i] = br.ReadBytes(pixelSize);

                for (int i = 0; i < pixelSize; ++i)
                    pixels[1][i] = br.ReadBytes(pixelSize);

                for (int i = 0; i < pixelSize; ++i)
                    pixels[2][i] = br.ReadBytes(pixelSize);

                ImageData dImage = new ImageData(pixels, lbl);
                Frame.Add(dImage);
                //Console.WriteLine(dImage.ToString());
                //Console.ReadLine();
            }
            // each image

            imageStream.Close();
        }

        private void ExtractMNIST()
        {
            string trainImages = @"C:\BDK\CNTK\Examples\Image\DataSets\MNIST\train-images-idx3-ubyte.gz";
            string trainLabels = @"C:\BDK\CNTK\Examples\Image\DataSets\MNIST\train-labels-idx1-ubyte.gz";
            GZipStream imageStream = new GZipStream(new FileStream(trainImages, FileMode.Open), CompressionMode.Decompress);
            GZipStream labelStream = new GZipStream(new FileStream(trainLabels, FileMode.Open), CompressionMode.Decompress);
            BinaryReader brimg = new BinaryReader(imageStream);
            BinaryReader brlbl = new BinaryReader(labelStream);
            int magic1 = brimg.ReadInt32(); // discard
            int numImages = brimg.ReadInt32();
            int numRows = brimg.ReadInt32();
            int numCols = brimg.ReadInt32();

            int magic2 = brlbl.ReadInt32();
            int numLabels = brlbl.ReadInt32();

            byte[][] pixels = new byte[28][];
            for (int i = 0; i < pixels.Length; ++i)
                pixels[i] = new byte[28];

            // each test image
            for (int di = 0; di < 60000; ++di)
            {
                for (int i = 0; i < 28; ++i)
                {
                    for (int j = 0; j < 28; ++j)
                    {
                        byte b = brimg.ReadByte();
                        pixels[i][j] = b;
                    }
                }

                byte lbl = brlbl.ReadByte();

                ImageData dImage = new ImageData(new byte[][][] { pixels }, lbl);
                Frame.Add(dImage);
                //Console.WriteLine(dImage.ToString());
                //Console.ReadLine();
            } // each image

            imageStream.Close();
            labelStream.Close();

            Console.ReadLine();
        }
    }

    public class ImageData
    {
        public byte[][][] Pixels;

        public byte label;

        public ImageData(byte[][][] pixels, byte label)
        {
            this.Pixels = pixels;
            this.label = label;
        }
    }
}
