﻿using Kinect_TP;
using Kinect_Utils;
using Microsoft.Kinect;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KinectConnection
{
    /// <summary>
    /// Classe représentant un flux d'image de profondeur pour la Kinect.
    /// </summary>
    public class DepthImageStream : KinectStream
    {

        private const int MapDepthToByte = 8000 / 256; // Comment convertir les valeurs de profondeur representé sur 8 octets

        private KinectSensor kinectSensor = null; // Capteur Kinect actif

        private DepthFrameReader depthFrameReader = null; // Lecteur pour les frames de profondeur

        private FrameDescription depthFrameDescription = null; // Description des données contenues dans la frame de profondeur

        private WriteableBitmap depthBitmap = null; // Bitmap à afficher

        private byte[] depthPixels = null; // Stockage intermédiaire pour les données de frame converties en couleur

        public override ImageSource ImageSource // Obtient la source d'image pour afficher.
        {
            get
            {
                return this.depthBitmap;
            }
        }

        /// <summary>
        /// Initialise une nouvelle instance de la classe DepthImageStream.
        /// </summary>
        public DepthImageStream(KinectManager manager) : base(manager)
        {
            // Obtenir la description de la frame depuis DepthFrameSource
            this.depthFrameDescription = this.Sensor.DepthFrameSource.FrameDescription;

            // Allouer de l'espace pour stocker les pixels reçus et convertis
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];

            // Créer la bitmap à afficher
            this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
        }

        /// <summary>
        /// Démarre la lecture du flux de profondeur.
        /// </summary>
        public override void Start()
        {
            if (this.Sensor != null)
            {
                // Ouvre le lecteur des frames de profondeur
                this.depthFrameReader = this.Sensor.DepthFrameSource.OpenReader();

                if (this.depthFrameReader != null)
                {
                    // Connecter le gestionnaire pour l'arrivée de la frame
                    this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;
                }
            }
        }

        /// <summary>
        /// Arrête la lecture du flux de profondeur.
        /// </summary>
        public override void Stop()
        {
            if (this.depthFrameReader != null)
            {
                this.depthFrameReader.FrameArrived -= this.Reader_FrameArrived;

                // Libérer le lecteur pour libérer les ressources.
                // Si nous ne le faisons pas manuellement, le GC le fera pour nous, mais nous ne savons pas quand.
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }
        }

        /// <summary>
        /// Méthode appelée lors de l'arrivée d'une nouvelle frame de profondeur.
        /// </summary>
        /// <param name="sender">objet émettant l'événement</param>
        /// <param name="e">arguments de l'événement</param>
        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // La manière la plus rapide de traiter les données d'index de corps est d'accéder directement au tampon sous-jacent
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // Vérifier les données et écrire les données de couleur dans la bitmap d'affichage
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            // Remarque : Pour voir l'ensemble de la plage de profondeur (y compris la profondeur de champ lointain moins fiable)
                            // nous réglons maxDepth sur la limite extrême de profondeur potentielle
                            ushort maxDepth = ushort.MaxValue;


                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();
            }
        }

        /// <summary>
        /// Accède directement au tampon d'image sous-jacent du DepthFrame
        /// pour créer une bitmap affichable.
        /// Cette méthode nécessite l'option du compilateur "/unsafe" pour avoir directement accès
        /// à la mémoire native pointée par le pointeur depthFrameData.
        /// </summary>
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // Les données de frame de profondeur sont des valeurs de 16 bits
            ushort* frameData = (ushort*)depthFrameData;

            // Convertir la profondeur en une représentation visuelle
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                // Obtenir la profondeur pour ce pixel
                ushort depth = frameData[i];

                // Pour convertir en un octet, nous mappions la valeur de profondeur à la plage d'octets.
                // Les valeurs en dehors de la plage de profondeur fiable sont mappées à 0 (noir).
                this.depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
        }

        /// <summary>
        /// Rend les pixels de profondeur dans la bitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);
        }
    }
}
