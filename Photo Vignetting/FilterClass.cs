using System;

namespace Photo_Vignetting
{
    //Data necessary for the vignetting operation will be stored in the class.
    public unsafe class FilterClass
    {

        //variables
        public byte* pointer;
        public byte* lastBit;
        public int correctRow;
        public int x;
        public int height;
        public double halfX;
        public double halfY;
        public double smallCircle;
        public double maxRadius;

        public int y = 0; // This variable will be a counter controlling if there are any pixels left in the row.
                          // It will also be useful in calculations


        //Constructor, gets all data while creating object
        public FilterClass(byte* imgPtr, byte* lastBit, double maxRadius, int correctRow, int x, int height, double halfX, double halfY, double circle)
        {
            this.pointer = imgPtr;
            this.lastBit = lastBit;
            this.correctRow = correctRow;
            this.x = x;
            this.height = height;
            this.halfX = halfX;
            this.halfY = halfY;
            this.smallCircle = circle;
            this.maxRadius = maxRadius;
        }


        //below the filter function, further math calculations and finally, using c# method to do the filter
        public unsafe void doFilter(Object threadContext)
        {
            // This will iterate through all bits assigned to the thread
            while (this.pointer + 2 <= this.lastBit)
            {
                //here it calculates the distance of the current pixel from the arbitrary center of the image
                double radius = ((this.y - this.halfX) * (this.y - this.halfX)) + ((this.height - this.halfY) * (this.height - this.halfY));
                //here it calculates the distance from the circle, which will be needed to calculate the strength of the filter, which should decrease as it gets closer to the center.
                double distanceFromCircle = ((this.y - this.halfX) * (this.y - this.halfX)) + ((this.height - this.halfY) * (this.height - this.halfY)) - this.smallCircle;

                if (this.smallCircle <= radius) //if true, filter will be applied on current pixel
                {
                    CsFilter(distanceFromCircle, maxRadius, pointer);
                }


                this.pointer += 3;
                //The next bit will be checked on the next loop iteration.
                this.y++;

                //below, we are checking if the variable 'y' won't go beyond the image, and if it does, we will use 'correctRow' to fix it.
                //Then, the next pixel (the corrected one) will be set in a new row
                if (this.y >= (int)(this.halfX * 2))
                {
                    this.pointer += this.correctRow;
                    this.y = 0; //new row starts
                    this.height++;

                }
            }
        }


        //this is a method that applies a filter on a chosen pixel. By doing this to all pixels, output photo will have a filter applied on it
        public unsafe void CsFilter(double distanceFromCircle, double maxRadius, byte* pointer)
        {

            //radius - distance of the current pixel from the arbitrary center of the image
            //distanceFromCircle - distance from the circle, will be needed to calculate the filter strength, which should be decreasing as the pixel gets closer to the center

            //here is the intensity of the effect.
            double strength = distanceFromCircle / maxRadius;

            //without the next instruction, the filter strength could be too high and the vignette color would take on other colors instead of black.
            //it would take on these colors because it would exceed the RGB value.
            if (strength > 1) strength = 1;

            //Here the filter is applied, each bit will take on more black color the further it is from the center.
            //the operation must be repeated three times because the colors are mixed from red, blue, and green.
            //when these three colors are mixed, black is created.
            *(pointer + 0) -= (byte)((*(pointer + 0)) * strength);
            *(pointer + 1) -= (byte)((*(pointer + 1)) * strength);
            *(pointer + 2) -= (byte)((*(pointer + 2)) * strength);
        }

    }
}
