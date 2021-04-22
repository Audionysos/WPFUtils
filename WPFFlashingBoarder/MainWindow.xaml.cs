using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WPFFlashingBorder {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		public MainWindow() {
			InitializeComponent();
		}
	}

	/// <summary>Example control which takes the border.</summary>
	public class Borderized : Grid {
		private Border b;

		public Borderized() {
			b = new Border();
			b.IsHitTestVisible = false;
			Background = Brushes.Black;
			b.BorderThickness = new Thickness(50, 50, 50, 100);
			b.BorderBrush = Brushes.Red;
			this.Children.Add(new Button() {
				Content = "Click Me!", Width = 200, Height = 50,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
			});
			this.Children.Add(b);
			var fb = new FlashingBorder(this);
			CompositionTarget.Rendering += onRender; //for color change only
		}

		#region Color fades
		double lc = 0;
		double cm = 0;
		double phase = 0;
		DateTime lastAlphaUpdate;
		private void onRender(object sender, EventArgs e) {
			//frequency = 1;
			var n = DateTime.Now;
			var d = n - lastAlphaUpdate;
			lastAlphaUpdate = n;
			var ds = d.TotalMilliseconds / 1000d; //delta seconds
			var fc = .05 * ds; //flashes count during last update
			phase += fc * 2; //times two to compensate two way (back-forward) flashing
			phase = phase % 2;
			cm = phase;
			if (cm > 1) cm = 2 - phase;

			//if (Math.Abs(lc - cm) < 0.01) { return; }
			lc = cm;
			cm *= 3;
			var f = 0; Color m = Colors.Black;
			if (cm < 1) { f = 0; m = Mix(Colors.Red, cm, Colors.LimeGreen); }
			else if (cm < 2) { f = 1; m = Mix(Colors.LimeGreen, cm - 1, Colors.Yellow); }
			else if(cm< 3) { f = 2; m = Mix(Colors.Yellow, cm - 2, Colors.Aquamarine); }
			b.BorderBrush = new SolidColorBrush(m);
			//Debug.WriteLine($"{cm} : {f} : {cm - f} :A{m.A}, R{m.R}, G{m.G}, B{m.B}");
		}

		//https://stackoverflow.com/questions/790699/wpf-color-interpolation
		//the idiot use swiched color sides, I lost over 30min figuring out why colors are jumping - better search my onw code next time
		/// <summary>
		/// Mixes factor*color1 with (1-factor)*color2. 
		/// </summary>
		public static Color Mix(Color color2, double factor, Color color1) {
			if (factor < 0) throw new Exception($"Factor {factor} must be greater equal 0.");
			if (factor > 1) throw new Exception($"Factor {factor} must be smaller equal 1.");

			if (factor == 0) return color2;
			if (factor == 1) return color1;

			var factor1 = 1 - factor;
			return Color.FromArgb(
			  (byte)((color1.A * factor + color2.A * factor1)),
			  (byte)((color1.R * factor + color2.R * factor1)),
			  (byte)((color1.G * factor + color2.G * factor1)),
			  (byte)((color1.B * factor + color2.B * factor1)));
		}
		#endregion
	}

	/// <summary>Create a gradient, flashing brush for a border of a given <see cref="FrameworkElement"/>.
	/// The <see cref="target"/> element have to contain a <see cref="Border"/> child for this object to work.
	/// The <see cref="FlashingBorder"/> takes color from original <see cref="Brush"/> that was set for a border.
	/// The <see cref="Border.BorderThickness"/> is also respected.
	/// At the moment the class don't implemnet any removing mechanism.</summary>
	public class FlashingBorder : UIElementExtension {
		/// <summary>Parent element which suppose to have a border.</summary>
		public FrameworkElement target { get; private set; }
		/// <summary>Frequency of flashing [Hz].</summary>
		public double frequency { get; set; } = 1;
		//public int duration { get; set; }

		private Border border;
		private Brush originalBrush;
		private Color colorA = Colors.Red;
		private Color colorB = Colors.Red;
		/// <summary>Output brush constructed to fit the border.</summary>
		private VisualBrush vb;

		public FlashingBorder() {}

		public FlashingBorder(FrameworkElement target) {
			this.target = target ?? throw new ArgumentNullException();
			border = getParentBorder();
			target.LayoutUpdated += onLayoutChange;
			createBrush();
			CompositionTarget.Rendering += onRender;
		}

		#region Alpha update
		private double alpha = 1;
		private DateTime lastAlphaUpdate;
		private double phase = 0;
		private void onRender(object sender, EventArgs e) {
			//frequency = 2;
			if(frequency == 0) { alpha = 1; return; }
			var n = DateTime.Now;
			var d = n - lastAlphaUpdate;
			lastAlphaUpdate = n;
			var ds = d.TotalMilliseconds / 1000d; //delta seconds
			var fc = frequency * ds; //flashes count during last update
			phase += fc * 2; //times two to compensate two way (back-forward) flashing
			phase = phase % 2;
			alpha = phase;
			if (alpha > 1) alpha = 2 - phase;
		}
		#endregion

		private Border getParentBorder() {
			//DependencyObject p = target;
			//while (p != null && (p as Border) == null) {
			//	p = VisualTreeHelper.GetParent(p);
			//}
			var cc = VisualTreeHelper.GetChildrenCount(target);
			for (int i = 0; i < cc; i++) {
				var c = VisualTreeHelper.GetChild(target, i);
				if (c is Border b) return b;
			}
			return null;
		}

		private void onLayoutChange(object sender, EventArgs e) {
			createBrush();
		}

		private void createBrush() {
			if (target == null) return;
			border = getParentBorder();
			if (border == null) return;
			trySetColorFromOriginalBorder();
			//colorB.A = 0;
			var br = getBorderRatio();
			var c = createBorderShapes(br);
			if (c == null) return;

			//var b = new LinearGradientBrush(Colors.Red, Colors.Green, 0);
			//g.Background = b;
			vb = new VisualBrush(c);
			vb.Opacity = alpha;
			border.BorderBrush = vb;
			//target.BorderThickness = new Thickness(20);
		}

		private void trySetColorFromOriginalBorder() {
			if (border.BorderBrush == vb) return;
			originalBrush = border.BorderBrush;
			var sb = originalBrush as SolidColorBrush;
			if (sb == null) return;
			var sc = sb.Color;
			colorA = sc;
			sc.A = 0;
			colorB = sc;
		}

		private Canvas createBorderShapes(Thickness br) {
			if (br == new Thickness(0)) return null; //no ratio available.
			var left = new Polygon() {
				Points = {
					new Point(0,0), new Point(br.Left, br.Top),
					new Point(br.Left, 1 - br.Bottom), new Point(0, 1),
				},
				Fill = new LinearGradientBrush(colorA, colorB, 0),
			};
			var right = new Polygon() {
				Points = {
					new Point(1, 0), new Point(1-br.Right, br.Top),
					new Point(1-br.Right, 1-br.Bottom), new Point(1, 1)
				},
				Fill = new LinearGradientBrush(colorB, colorA, 0),
			};
			var top = new Polygon() {
				Points = {
					new Point(0,0), new Point(1, 0),
					new Point(1-br.Right, br.Top), new Point(br.Left, br.Top)
				},
				Fill = new LinearGradientBrush(colorA, colorB, 90),
			};
			var bottom = new Polygon() {
				Points = {
					new Point(br.Left, 1-br.Bottom), new Point(1-br.Right, 1-br.Bottom),
					new Point(1, 1), new Point(0, 1)
				},
				Fill = new LinearGradientBrush(colorB, colorA, 90),
			};
			var c = new Canvas() { Width = 200, Height = 200, //canvas dimensions don't really matter here
				Children = {left, right, top, bottom }
			};
			return c;
		}

		/// <summary>Returns border thicknes as the ration </summary>
		private Thickness getBorderRatio() {
			if (target.ActualWidth == 0 || target.ActualHeight == 0)
				return new Thickness(0);
			var t = border.BorderThickness;
			return new Thickness(
				t.Left / target.ActualWidth,
				t.Top / target.ActualHeight,
				t.Right / target.ActualWidth,
				t.Bottom / target.ActualHeight);
		}

		#region XAML BS, not really needed
		protected override void update(UIElementExtensions ex) {
			target = ex.Parent as FrameworkElement;
			createBrush();
		}

		protected internal override void parentChanged(UIElementExtensions e, DependencyObject oldParent) {
			base.parentChanged(e, oldParent);
			target = e.Parent as FrameworkElement;
			createBrush();
		}

		protected internal override void renderSizeChanged(UIElementExtensions e, SizeChangedInfo info) {
			base.renderSizeChanged(e, info);
			createBrush();
		}
		#endregion

	}

	#region XAML crap, not required

	public class GradientBorder : Decorator {

		private UIElement _c;
		public UIElement Content {
			get => _c;
			set {
				_c = value;
			}
		}

		public GradientBorder() {

		}

		protected override void OnRender(DrawingContext dc) {
			var fe = new FrameworkElement();
		}
	}

	/// <summary>I don't want <see cref="FlashingBorder"/> to take all <see cref="UIElement"/> shit which will hide key <see cref="FlashingBorder"/> properties, so I started this container for it.
	/// The border could be add to XAML but for some reason, the result is only visible at runtime, so this is basically pointless...</summary>
	[ContentProperty("Children")]
	public class UIElementExtensions : UIElement {
		[TypeConverter(typeof(FlashingBorderConverter))]
		public ObservableCollection<UIElementExtension> Children { get; set; } = new ObservableCollection<UIElementExtension>();
		public DependencyObject Parent => VisualParent;

		public UIElementExtensions() {
			Children.CollectionChanged += onCollectionChanged;
		}

		private void onCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
			foreach (var ch in Children) {

			}
		}

		protected override void OnVisualParentChanged(DependencyObject oldParent) {
			base.OnVisualParentChanged(oldParent);
			if (Children == null) return;
			foreach (var ch in Children) ch.parentChanged(this, oldParent);
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo info) {
			base.OnRenderSizeChanged(info);
			if (Children == null) return;
			foreach (var ch in Children) ch.renderSizeChanged(this, info);
		}
	}

	public abstract class UIElementExtension {
		protected internal virtual void parentChanged(UIElementExtensions e, DependencyObject oldParent) { }
		protected internal virtual void renderSizeChanged(UIElementExtensions e, SizeChangedInfo info) { }

		protected abstract void update(UIElementExtensions ex);
	}

	public class FlashingBorderConverter : TypeConverter {
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
			if (typeof(UIElementExtension).IsAssignableFrom(sourceType)) return true;
			return false;
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
			var e = value as UIElementExtension;
			return e;
		}

	}
	#endregion

}
