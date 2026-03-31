import Header from './Header';
import Navigation from './Navigation';
import Footer from './Footer';

const Layout = ({ children, userRole, openVkladka, activeTab }) => {
    return (
        <div className="page-container">
            <Header userRole={userRole} />
            <Navigation openVkladka={openVkladka} activeTab={activeTab} />
            <main>
                {children}
            </main>
            <Footer />
        </div>
    );
};

export default Layout; 