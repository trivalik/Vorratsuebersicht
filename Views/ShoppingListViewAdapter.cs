
using System;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;

namespace VorratsUebersicht
{
    public class ShoppingListViewAdapter : BaseAdapter<ShoppingListView>
    {
        List<ShoppingListView> items;
        Activity context;

        public ShoppingListViewAdapter(Activity context, List<ShoppingListView> items) : base()
        {
            this.context = context;
            this.items = items;
        }
        public override long GetItemId(int position)
        {
            return position;
        }
        public override ShoppingListView this[int position]
        {
            get { return items[position]; }
        }
        public override int Count
        {
            get { return items.Count; }
        }
        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var item = items[position];
            View view = convertView;
            if (view == null) // no view to re-use, create new
            {
                view = context.LayoutInflater.Inflate(Resource.Layout.ArticleListView, null);
            }

            view.FindViewById<TextView>(Resource.Id.Text1).Text = item.Heading;
            view.FindViewById<TextView>(Resource.Id.Text2).Text = item.SubHeading;
            view.FindViewById<TextView>(Resource.Id.Text3).Text = item.Information;
            view.FindViewById<TextView>(Resource.Id.Text3).Visibility = ViewStates.Visible;

            ImageView image = view.FindViewById<ImageView>(Resource.Id.Image);
            if (item.Image != null)
            {
                image.Tag = item.ArticleId;
                image.Click -= OnImageClicked;
                image.Click += OnImageClicked;
            }

            if (item.Image == null)
                image.SetImageResource(Resource.Drawable.ic_photo_camera_black_24dp);
            else
                view.FindViewById<ImageView>(Resource.Id.Image).SetImageBitmap(item.Image);

            return view;
       }

        private void OnImageClicked(object sender, EventArgs e)
        {
            ImageView imageToView = (ImageView)sender;

            int articleId = (int)imageToView.Tag;

            var articleImage = new Intent(context, typeof(ArticleImageActivity));
            articleImage.PutExtra("ArticleId", articleId);
            context.StartActivity(articleImage);
        }
    }
}