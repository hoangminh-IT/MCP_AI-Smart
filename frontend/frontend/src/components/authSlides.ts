import warehouse from '../assets/auth/warehouse.jpg';
import receiving from '../assets/auth/receiving.jpg';
import inventoryCheck from '../assets/auth/inventory-check.jpg';

// Photo credits and source URLs are kept beside the bundled image assets.
export const authSlides = [
  {
    id: 'warehouse',
    src: warehouse,
    alt: 'Các dãy kệ chứa hàng và pallet trong kho',
    position: '50% 50%',
  },
  {
    id: 'receiving',
    src: receiving,
    alt: 'Nhân viên vận hành xe nâng để di chuyển hàng hóa',
    position: '60% 45%',
  },
  {
    id: 'inventory-check',
    src: inventoryCheck,
    alt: 'Nhân viên dùng máy quét mã vạch và máy tính bảng kiểm tra hàng trong kho',
    position: '50% 40%',
  },
] as const;
